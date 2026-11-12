using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Tests;

/// <summary>
/// Pruebas exhaustivas de dominio para Entrega Parcial de Bultos (cambio de requerimientos 2.1).
/// </summary>
public class EntregaParcialDominioTests
{
    private static ReglaOperativa CrearReglaOperativa(
        bool admiteEntregaParcial = true,
        string politicaEntregaParcial = ReglaOperativa.PoliticaNuevaVisitaSinRecargo,
        string tipoPrueba = "Firma") =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            DateTime.UtcNow.AddDays(-1),
            maxIntentosEntrega: 3,
            plazoEntreIntentos: TimeSpan.FromHours(24),
            tipoPruebaEntregaExigida: tipoPrueba,
            politicaDevolucion: "Retorno a depósito",
            plazoComprometidoEstandar: TimeSpan.FromHours(48),
            plazoComprometidoUrgente: TimeSpan.FromHours(24),
            admiteEntregaParcial: admiteEntregaParcial,
            politicaEntregaParcial: politicaEntregaParcial);

    private static Envio CrearEnvioEnTransitoConTresBultos(out Guid zonaId)
    {
        var operadorId = Guid.NewGuid();
        var comercioOperadorId = Guid.NewGuid();
        var direccionId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        zonaId = Guid.NewGuid();

        var envio = new Envio(
            Guid.NewGuid(),
            comercioOperadorId,
            Guid.NewGuid(),
            "Estandar",
            "ENV-PARCIAL-001",
            direccionEntregaId: direccionId,
            direccionDestino: "Av. 18 de Julio 1234",
            nombreDestinatario: "María Rodríguez",
            operadorId: operadorId);

        envio.AgregarBulto("BUL-001", 4m, 20, 20, 20);
        envio.AgregarBulto("BUL-002", 5m, 20, 20, 20);
        envio.AgregarBulto("BUL-003", 6m, 20, 20, 20);
        envio.AplicarTarifa(200m, Guid.NewGuid());

        envio.RecepcionarEnDeposito(new[] { "BUL-001", "BUL-002", "BUL-003" }, null, null, usuarioId);
        envio.ClasificarBultos(new[]
        {
            new ClasificacionBulto("BUL-001", PrioridadBulto.Normal, zonaId, "Paquete"),
            new ClasificacionBulto("BUL-002", PrioridadBulto.Normal, zonaId, "Paquete"),
            new ClasificacionBulto("BUL-003", PrioridadBulto.Alta, zonaId, "Fragil")
        }, usuarioId);

        var rutaId = Guid.NewGuid();
        envio.AsignarRuta(rutaId, usuarioId);
        envio.DespacharEnRuta(rutaId, usuarioId);

        return envio;
    }

    [Fact]
    public void MaquinaEstadosBulto_TablaDeclarativa_ContieneTodosLosEstadosYValidaTransiciones()
    {
        Assert.True(MaquinaEstadosBulto.TablaTransiciones.ContainsKey(MaquinaEstadosBulto.Ingresado));
        Assert.True(MaquinaEstadosBulto.TablaTransiciones.ContainsKey(MaquinaEstadosBulto.CargadoEnVehiculo));
        Assert.True(MaquinaEstadosBulto.TablaTransiciones.ContainsKey(MaquinaEstadosBulto.Entregado));
        Assert.True(MaquinaEstadosBulto.TablaTransiciones.ContainsKey(MaquinaEstadosBulto.NoEntregado));
        Assert.True(MaquinaEstadosBulto.TablaTransiciones.ContainsKey(MaquinaEstadosBulto.PendienteReenvio));
        Assert.True(MaquinaEstadosBulto.TablaTransiciones.ContainsKey(MaquinaEstadosBulto.Devuelto));

        Assert.True(MaquinaEstadosBulto.EsTransicionValida(MaquinaEstadosBulto.CargadoEnVehiculo, MaquinaEstadosBulto.Entregado));
        Assert.True(MaquinaEstadosBulto.EsTransicionValida(MaquinaEstadosBulto.CargadoEnVehiculo, MaquinaEstadosBulto.NoEntregado));
        Assert.True(MaquinaEstadosBulto.EsTransicionValida(MaquinaEstadosBulto.NoEntregado, MaquinaEstadosBulto.PendienteReenvio));

        Assert.True(MaquinaEstadosBulto.EsEstadoTerminal(MaquinaEstadosBulto.Entregado));
        Assert.True(MaquinaEstadosBulto.EsEstadoTerminal(MaquinaEstadosBulto.Devuelto));
        Assert.False(MaquinaEstadosBulto.EsTransicionValida(MaquinaEstadosBulto.Entregado, MaquinaEstadosBulto.NoEntregado));
    }

    [Fact]
    public void RegistrarEntregaParcial_DosDeTresBultos_DerivaEstadoEntregadoParcialmenteYAsociaPruebaSoloAEntregados()
    {
        var envio = CrearEnvioEnTransitoConTresBultos(out _);
        var regla = CrearReglaOperativa(admiteEntregaParcial: true, politicaEntregaParcial: ReglaOperativa.PoliticaNuevaVisitaSinRecargo);
        var motivoDanado = new MotivoNoEntrega(Guid.NewGuid(), envio.OperadorId, "BUL_DANADO", "Bulto con daño en empaque", false);

        var resultado = envio.RegistrarEntregaParcial(
            Guid.NewGuid(),
            regla,
            new[] { "BUL-001", "BUL-002" },
            new[] { new BultoNoEntregadoInput("BUL-003", motivoDanado, "Rechazado por caja abollada") },
            "María Rodríguez",
            "4.111.222-3",
            firmaDigitalBase64: "firma-base64",
            fotoEvidenciaBase64: null,
            latitud: -34.90,
            longitud: -56.18,
            fechaHoraDispositivoUtc: DateTime.UtcNow);

        Assert.Equal(EstadoEnvio.EntregadoParcialmente, envio.EstadoActual);
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, resultado.EstadoFinalEnvio);
        Assert.Equal(3, resultado.TotalBultosOperativos);
        Assert.Equal(2, resultado.BultosEntregadosAcumulados);
        Assert.Equal(1, resultado.BultosPendientes);

        var b1 = envio.Bultos.Single(b => b.Identificacion == "BUL-001");
        var b2 = envio.Bultos.Single(b => b.Identificacion == "BUL-002");
        var b3 = envio.Bultos.Single(b => b.Identificacion == "BUL-003");

        Assert.Equal(MaquinaEstadosBulto.Entregado, b1.EstadoBulto);
        Assert.Equal(MaquinaEstadosBulto.Entregado, b2.EstadoBulto);
        Assert.Equal(MaquinaEstadosBulto.PendienteReenvio, b3.EstadoBulto);
        Assert.Equal("BUL_DANADO", b3.MotivoNoEntregaCodigo);

        // La prueba de entrega queda asociada exclusivamente a los bultos efectivamente entregados
        Assert.Equal(resultado.Prueba.Id, b1.PruebaDeEntregaId);
        Assert.Equal(resultado.Prueba.Id, b2.PruebaDeEntregaId);
        Assert.Null(b3.PruebaDeEntregaId);
        Assert.Equal(new[] { "BUL-001", "BUL-002" }, resultado.Prueba.CodigosBultosEntregados);
    }

    [Fact]
    public void RegistrarEntregaParcial_CuandoOperadorNoAdmiteEntregaParcial_LanzaExcepcionDeDominio()
    {
        var envio = CrearEnvioEnTransitoConTresBultos(out _);
        var regla = CrearReglaOperativa(admiteEntregaParcial: false);
        var motivo = new MotivoNoEntrega(Guid.NewGuid(), envio.OperadorId, "RECHAZO_PARCIAL", "Rechazo parcial", false);

        var ex = Assert.Throws<DomainValidationException>(() =>
            envio.RegistrarEntregaParcial(
                Guid.NewGuid(),
                regla,
                new[] { "BUL-001", "BUL-002" },
                new[] { new BultoNoEntregadoInput("BUL-003", motivo) },
                "María Rodríguez",
                "4.111.222-3",
                firmaDigitalBase64: "firma-base64",
                fotoEvidenciaBase64: null,
                latitud: null,
                longitud: null,
                fechaHoraDispositivoUtc: DateTime.UtcNow));

        Assert.Contains("no admite entregas parciales", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(EstadoEnvio.EnTransito, envio.EstadoActual);
    }

    [Fact]
    public void RegistrarEntregaParcial_ConPoliticaNuevaVisitaConRecargo_AplicaRecargoYDejaBultoDisponibleParaNuevaRuta()
    {
        var envio = CrearEnvioEnTransitoConTresBultos(out _);
        decimal tarifaOriginal = envio.TarifaCalculada;
        var regla = CrearReglaOperativa(
            admiteEntregaParcial: true,
            politicaEntregaParcial: ReglaOperativa.PoliticaNuevaVisitaConRecargo);
        var motivo = new MotivoNoEntrega(Guid.NewGuid(), envio.OperadorId, "FALTANTE_VEHICULO", "No subió al vehículo", false);

        var resultado = envio.RegistrarEntregaParcial(
            Guid.NewGuid(),
            regla,
            new[] { "BUL-001", "BUL-002" },
            new[] { new BultoNoEntregadoInput("BUL-003", motivo) },
            "María Rodríguez",
            "4.111.222-3",
            firmaDigitalBase64: "firma-base64",
            fotoEvidenciaBase64: null,
            latitud: null,
            longitud: null,
            fechaHoraDispositivoUtc: DateTime.UtcNow);

        Assert.True(resultado.RecargoAplicado);
        Assert.True(envio.TarifaCalculada > tarifaOriginal);

        // El bulto pendiente queda disponible para asignarse a una nueva hoja de ruta
        var b3 = envio.Bultos.Single(b => b.Identificacion == "BUL-003");
        Assert.True(b3.EstaDisponibleParaRuta);
        Assert.Null(envio.ObtenerMotivoNoAsignable());
        Assert.Equal(6m, envio.PesoTotal); // Solo pesa el bulto pendiente (BUL-003 = 6kg)

        var nuevaRutaId = Guid.NewGuid();
        envio.AsignarRuta(nuevaRutaId, Guid.NewGuid());
        envio.DespacharEnRuta(nuevaRutaId, Guid.NewGuid());

        Assert.Equal(EstadoEnvio.EnTransito, envio.EstadoActual);
        Assert.Equal(MaquinaEstadosBulto.Entregado, envio.Bultos.Single(b => b.Identificacion == "BUL-001").EstadoBulto);
        Assert.Equal(MaquinaEstadosBulto.Entregado, envio.Bultos.Single(b => b.Identificacion == "BUL-002").EstadoBulto);
        Assert.Equal(MaquinaEstadosBulto.CargadoEnVehiculo, b3.EstadoBulto);
    }

    [Fact]
    public void RegistrarEntregaParcial_ConPoliticaDevolucionAlComercio_MarcaBultoPendienteEnRetornoADeposito()
    {
        var envio = CrearEnvioEnTransitoConTresBultos(out _);
        var regla = CrearReglaOperativa(
            admiteEntregaParcial: true,
            politicaEntregaParcial: ReglaOperativa.PoliticaDevolucionAlComercio);
        var motivo = new MotivoNoEntrega(Guid.NewGuid(), envio.OperadorId, "RECHAZO_DESTINATARIO", "Rechazado por comprador", false);

        envio.RegistrarEntregaParcial(
            Guid.NewGuid(),
            regla,
            new[] { "BUL-001" },
            new[]
            {
                new BultoNoEntregadoInput("BUL-002", motivo),
                new BultoNoEntregadoInput("BUL-003", motivo)
            },
            "María Rodríguez",
            "4.111.222-3",
            firmaDigitalBase64: "firma-base64",
            fotoEvidenciaBase64: null,
            latitud: null,
            longitud: null,
            fechaHoraDispositivoUtc: DateTime.UtcNow);

        Assert.Equal(EstadoEnvio.EntregadoParcialmente, envio.EstadoActual);
        Assert.Equal(MaquinaEstadosBulto.EnRetornoADeposito, envio.Bultos.Single(b => b.Identificacion == "BUL-002").EstadoBulto);
        Assert.Equal(MaquinaEstadosBulto.EnRetornoADeposito, envio.Bultos.Single(b => b.Identificacion == "BUL-003").EstadoBulto);
        // Al ir a devolución al comercio, no quedan disponibles para una nueva ruta de entrega
        Assert.NotNull(envio.ObtenerMotivoNoAsignable());
    }

    [Fact]
    public void ResolucionDeConflictosOfflinePorBulto_PreservaBultoYaEntregadoYPromueveBultoPendienteConPrueba()
    {
        var envio = CrearEnvioEnTransitoConTresBultos(out _);
        var regla = CrearReglaOperativa();
        var motivo = new MotivoNoEntrega(Guid.NewGuid(), envio.OperadorId, "NO_ESTABA", "Sin respuesta parcial", false);

        // Primera sincronización: entrega BUL-001 y deja BUL-002 y BUL-003 pendientes
        envio.RegistrarEntregaParcial(
            Guid.NewGuid(),
            regla,
            new[] { "BUL-001" },
            new[]
            {
                new BultoNoEntregadoInput("BUL-002", motivo),
                new BultoNoEntregadoInput("BUL-003", motivo)
            },
            "María Rodríguez",
            "4.111.222-3",
            firmaDigitalBase64: "firma-1",
            fotoEvidenciaBase64: null,
            latitud: null,
            longitud: null,
            fechaHoraDispositivoUtc: DateTime.UtcNow);

        // Segunda sincronización offline con conflicto: intenta reportar BUL-001 (ya entregado) como no entregado,
        // y entrega efectivamente BUL-002 y BUL-003 con prueba de entrega válida
        var resultadoSync = envio.RegistrarEntregaParcial(
            Guid.NewGuid(),
            regla,
            new[] { "BUL-002", "BUL-003" },
            new[] { new BultoNoEntregadoInput("BUL-001", motivo) },
            "María Rodríguez",
            "4.111.222-3",
            firmaDigitalBase64: "firma-2",
            fotoEvidenciaBase64: null,
            latitud: null,
            longitud: null,
            fechaHoraDispositivoUtc: DateTime.UtcNow);

        // Todos los bultos quedan entregados y el envío deriva declarativamente a Entregado
        Assert.Equal(EstadoEnvio.Entregado, envio.EstadoActual);
        Assert.Equal(EstadoEnvio.Entregado, resultadoSync.EstadoFinalEnvio);
        Assert.NotEmpty(resultadoSync.ConflictosResueltos);
        Assert.All(envio.Bultos, b => Assert.True(b.EstaEntregado));
    }

    [Fact]
    public void RegistrarEntregaParcial_SinMotivoParaBultoNoEntregado_LanzaExcepcionDeDominio()
    {
        var envio = CrearEnvioEnTransitoConTresBultos(out _);
        var regla = CrearReglaOperativa();

        var ex = Assert.Throws<DomainValidationException>(() =>
            envio.RegistrarEntregaParcial(
                Guid.NewGuid(),
                regla,
                new[] { "BUL-001", "BUL-002" },
                Array.Empty<BultoNoEntregadoInput>(),
                "María Rodríguez",
                "4.111.222-3",
                firmaDigitalBase64: "firma-1",
                fotoEvidenciaBase64: null,
                latitud: null,
                longitud: null,
                fechaHoraDispositivoUtc: DateTime.UtcNow));

        Assert.Contains("BUL-003", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

