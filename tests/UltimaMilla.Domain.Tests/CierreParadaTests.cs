using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;
using Xunit;

namespace UltimaMilla.Domain.Tests;

public class CierreParadaTests
{
    private readonly Guid _operadorId = Guid.NewGuid();
    private readonly Guid _comercioId = Guid.NewGuid();

    private ReglaOperativa CrearRegla(int maxIntentos = 3, string tipoPrueba = "Firma digital del receptor y Foto del paquete en domicilio")
    {
        return new ReglaOperativa(
            Guid.NewGuid(),
            _operadorId,
            version: 1,
            vigenteDesde: DateTime.UtcNow.AddDays(-10),
            maxIntentosEntrega: maxIntentos,
            plazoEntreIntentos: TimeSpan.FromHours(24),
            tipoPruebaEntregaExigida: tipoPrueba,
            politicaDevolucion: "Devolución automática a depósito",
            plazoComprometidoEstandar: TimeSpan.FromHours(48),
            plazoComprometidoUrgente: TimeSpan.FromHours(24));
    }

    private Envio CrearEnvioEnTransito(Guid rutaId)
    {
        var envio = new Envio(
            Guid.NewGuid(),
            _operadorId,
            Guid.NewGuid(),
            "Estandar",
            "TRK-TEST001");

        envio.TransicionarEstado(EstadoEnvio.EnDeposito, "RecepcionEnDeposito");
        envio.AsignarRuta(rutaId);
        envio.DespacharEnRuta(rutaId);
        return envio;
    }

    [Fact]
    public void UC20_EntregaExitosa_ConPruebaValida_TransicionaAEntregadoYGeneraPruebaEIntento()
    {
        // Arrange
        var rutaId = Guid.NewGuid();
        var paradaId = Guid.NewGuid();
        var envio = CrearEnvioEnTransito(rutaId);
        var regla = CrearRegla(tipoPrueba: "Firma digital del receptor");
        var fechaDispositivo = DateTime.UtcNow;

        // Act
        var intento = envio.RegistrarEntregaExitosa(
            paradaId,
            regla,
            nombreReceptor: "Juan Pérez",
            documentoReceptor: "1.234.567-8",
            firmaDigitalBase64: "base64_data_signature",
            fotoEvidenciaBase64: null,
            latitud: -34.9011,
            longitud: -56.1645,
            fechaHoraDispositivoUtc: fechaDispositivo);

        // Assert
        Assert.Equal(EstadoEnvio.Entregado, envio.EstadoActual);
        Assert.Equal("Exitoso", intento.Resultado);
        Assert.NotNull(intento.PruebaDeEntrega);
        Assert.Equal("Juan Pérez", intento.PruebaDeEntrega.NombreReceptor);
        Assert.Equal("1.234.567-8", intento.PruebaDeEntrega.DocumentoReceptor);
        Assert.Equal("base64_data_signature", intento.PruebaDeEntrega.FirmaImagenUrl);
        Assert.Single(envio.IntentosEntrega);

        // Eventos inmutables
        var ultimoEvento = envio.Eventos.Last();
        Assert.Equal("EntregaExitosa", ultimoEvento.TipoEvento);
        Assert.Equal(EstadoEnvio.Entregado, ultimoEvento.EstadoNuevo);
        Assert.Equal(EstadoEnvio.EnTransito, ultimoEvento.EstadoAnterior);
    }

    [Fact]
    public void UC20_EntregaExitosa_FaltandoFirmaExigidaPorRegla_LanzaDomainValidationException()
    {
        // Arrange
        var rutaId = Guid.NewGuid();
        var paradaId = Guid.NewGuid();
        var envio = CrearEnvioEnTransito(rutaId);
        var regla = CrearRegla(tipoPrueba: "Firma digital obligatoria");

        // Act & Assert
        var ex = Assert.Throws<DomainValidationException>(() =>
            envio.RegistrarEntregaExitosa(
                paradaId,
                regla,
                nombreReceptor: "Juan Pérez",
                documentoReceptor: "1.234.567-8",
                firmaDigitalBase64: null, // Falta firma requerida
                fotoEvidenciaBase64: null,
                latitud: -34.9011,
                longitud: -56.1645,
                fechaHoraDispositivoUtc: DateTime.UtcNow));

        Assert.Contains("firma digital obligatoria", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(EstadoEnvio.EnTransito, envio.EstadoActual);
    }

    [Fact]
    public void UC20_EntregaExitosa_EnvioNoEnTransito_LanzaDomainValidationException()
    {
        // Arrange
        var envio = new Envio(Guid.NewGuid(), _operadorId, Guid.NewGuid(), "Estandar", "TRK-ADMITIDO");
        var regla = CrearRegla();

        // Act & Assert (en estado Admitido, no EnTransito)
        Assert.Throws<DomainValidationException>(() =>
            envio.RegistrarEntregaExitosa(
                Guid.NewGuid(),
                regla,
                "Pedro",
                "123",
                "firma",
                "foto",
                0,
                0,
                DateTime.UtcNow));
    }

    [Fact]
    public void UC21_IntentoFallido_PrimerIntento_TransicionaANoEntregadoYLuegoAReprogramado()
    {
        // Arrange
        var rutaId = Guid.NewGuid();
        var paradaId = Guid.NewGuid();
        var envio = CrearEnvioEnTransito(rutaId);
        var regla = CrearRegla(maxIntentos: 3);
        var motivo = new MotivoNoEntrega(Guid.NewGuid(), _operadorId, "AUSENTE", "Cliente ausente en domicilio");

        // Act
        var (intento, estadoFinal, restantes) = envio.RegistrarIntentoFallido(
            paradaId,
            motivo,
            regla,
            observaciones: "Se tocó timbre 3 veces y se llamó al teléfono registrado sin respuesta.",
            fotoEvidenciaBase64: "base64_fachada_jpg",
            latitud: -34.9011,
            longitud: -56.1645,
            fechaHoraDispositivoUtc: DateTime.UtcNow);

        // Assert
        Assert.Equal(EstadoEnvio.Reprogramado, estadoFinal);
        Assert.Equal(EstadoEnvio.Reprogramado, envio.EstadoActual);
        Assert.Equal(2, restantes);
        Assert.Equal("Fallido", intento.Resultado);
        Assert.NotNull(intento.PruebaDeEntrega);
        Assert.Equal(1, envio.CantidadIntentos);

        // Validar doble transición en el historial de eventos append-only
        var eventos = envio.Eventos.ToList();
        var eventoPaso1 = eventos[^2];
        var eventoPaso2 = eventos[^1];

        Assert.Equal("IntentoEntregaFallido", eventoPaso1.TipoEvento);
        Assert.Equal(EstadoEnvio.NoEntregado, eventoPaso1.EstadoNuevo);

        Assert.Equal("ResolucionReglaOperativa_Reprogramado", eventoPaso2.TipoEvento);
        Assert.Equal(EstadoEnvio.Reprogramado, eventoPaso2.EstadoNuevo);
    }

    [Fact]
    public void UC21_IntentoFallido_AlcanzaMaximoIntentos_TransicionaAEnDevolucion()
    {
        // Arrange
        var rutaId = Guid.NewGuid();
        var paradaId = Guid.NewGuid();
        var envio = CrearEnvioEnTransito(rutaId);
        var regla = CrearRegla(maxIntentos: 2); // Máximo 2 intentos
        var motivo = new MotivoNoEntrega(Guid.NewGuid(), _operadorId, "DIRECCION_ERRONEA", "Dirección inexistente");

        // Act 1: Primer intento fallido -> Reprogramado
        envio.RegistrarIntentoFallido(paradaId, motivo, regla, "Primer intento fallido", null, null, null, DateTime.UtcNow);
        Assert.Equal(EstadoEnvio.Reprogramado, envio.EstadoActual);

        // Para simular el segundo intento, vuelve a ponerse EnTransito para la siguiente ruta
        envio.AsignarRuta(Guid.NewGuid());
        envio.DespacharEnRuta(rutaId);

        // Act 2: Segundo intento fallido -> Debe pasar a EnDevolucion
        var (intento2, estadoFinal2, restantes2) = envio.RegistrarIntentoFallido(
            paradaId,
            motivo,
            regla,
            observaciones: "Segundo intento: la calle indicada no existe según catastro.",
            fotoEvidenciaBase64: null,
            latitud: null,
            longitud: null,
            fechaHoraDispositivoUtc: DateTime.UtcNow);

        // Assert
        Assert.Equal(EstadoEnvio.EnDevolucion, estadoFinal2);
        Assert.Equal(EstadoEnvio.EnDevolucion, envio.EstadoActual);
        Assert.Equal(0, restantes2);
        Assert.Equal(2, envio.CantidadIntentos);

        var ultimoEvento = envio.Eventos.Last();
        Assert.Equal("ResolucionReglaOperativa_EnDevolucion", ultimoEvento.TipoEvento);
        Assert.Equal(EstadoEnvio.EnDevolucion, ultimoEvento.EstadoNuevo);
    }

    [Fact]
    public void UC21_IntentoFallido_ConMotivoInactivo_LanzaDomainValidationException()
    {
        // Arrange
        var rutaId = Guid.NewGuid();
        var paradaId = Guid.NewGuid();
        var envio = CrearEnvioEnTransito(rutaId);
        var regla = CrearRegla();
        var motivoInactivo = new MotivoNoEntrega(Guid.NewGuid(), _operadorId, "OBSOLETO", "Motivo no vigente");
        motivoInactivo.Desactivar();

        // Act & Assert
        var ex = Assert.Throws<DomainValidationException>(() =>
            envio.RegistrarIntentoFallido(
                paradaId,
                motivoInactivo,
                regla,
                "Intento con motivo inactivo",
                null,
                null,
                null,
                DateTime.UtcNow));

        Assert.Contains("inactivo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HojaDeRuta_CierreParadas_ActualizaEstadoParadaYFinalizaRutaCuandoTodasCompletadas()
    {
        // Arrange
        var ruta = new HojaDeRuta(Guid.NewGuid(), _operadorId, DateOnly.FromDateTime(DateTime.Today));
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());

        var envioId1 = Guid.NewGuid();
        var envioId2 = Guid.NewGuid();
        ruta.AgregarParada(envioId1, 1, Guid.NewGuid());
        ruta.AgregarParada(envioId2, 2, Guid.NewGuid());
        ruta.Liberar();
        ruta.RegistrarCargaEscaneada(new[] { Guid.NewGuid() });
        ruta.Despachar();

        var parada1 = ruta.Paradas.First(p => p.EnvioId == envioId1);
        var parada2 = ruta.Paradas.First(p => p.EnvioId == envioId2);

        // Act 1: Cerrar parada 1 como entrega exitosa
        ruta.RegistrarEntregaExitosaEnParada(parada1.Id);
        Assert.Equal("Entregada", parada1.Estado);
        Assert.Equal("Despachada", ruta.Estado); // Todavía queda la parada 2 pendiente

        // Act 2: Cerrar parada 2 como intento fallido
        ruta.RegistrarIntentoFallidoEnParada(parada2.Id);
        Assert.Equal("Fallida", parada2.Estado);

        // Assert: Al estar todas las paradas completadas, la ruta pasa automáticamente a Finalizada
        Assert.Equal("Finalizada", ruta.Estado);
    }
}
