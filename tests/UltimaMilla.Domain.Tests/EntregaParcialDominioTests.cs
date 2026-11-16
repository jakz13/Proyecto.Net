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
    private static readonly Guid OperadorId = Guid.NewGuid();

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
            "41112223",
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
                "41112223",
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
            "41112223",
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
            "41112223",
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
            "41112223",
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
            "41112223",
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
                "41112223",
                firmaDigitalBase64: "firma-1",
                fotoEvidenciaBase64: null,
                latitud: null,
                longitud: null,
                fechaHoraDispositivoUtc: DateTime.UtcNow));

        Assert.Contains("BUL-003", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReglaOperativa_TipoPruebaCompleta_ExigeFirmaFotoYDocumento()
    {
        var regla = CrearReglaOperativa(tipoPrueba: "Completa");

        Assert.Throws<DomainValidationException>(() => regla.ValidarPruebaEntrega(null, "foto-ok", "41112223"));
        Assert.Throws<DomainValidationException>(() => regla.ValidarPruebaEntrega("firma-ok", null, "41112223"));
        Assert.Throws<DomainValidationException>(() => regla.ValidarPruebaEntrega("firma-ok", "foto-ok", null));

        regla.ValidarPruebaEntrega("firma-ok", "foto-ok", "41112223");
    }

    [Fact]
    public void EntregaParcial_PoliticaDevolucionAlComercio_PermiteConfirmarRecepcionDeBultosDevueltosMantenidoEstadoParcial()
    {
        var envio = CrearEnvioEnTransitoConTresBultos(out _);
        var regla = CrearReglaOperativa(
            admiteEntregaParcial: true,
            politicaEntregaParcial: ReglaOperativa.PoliticaDevolucionAlComercio);
        var motivo = new MotivoNoEntrega(Guid.NewGuid(), envio.OperadorId, "RECHAZADO", "Rechazado por destinatario", false);

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
            "41112223",
            firmaDigitalBase64: "firma-1",
            fotoEvidenciaBase64: null,
            latitud: null,
            longitud: null,
            fechaHoraDispositivoUtc: DateTime.UtcNow);

        envio.ConfirmarDevolucionRecibida(Guid.NewGuid());

        Assert.Equal(EstadoEnvio.EntregadoParcialmente, envio.EstadoActual);
        Assert.Equal(MaquinaEstadosBulto.Entregado, envio.Bultos.Single(b => b.Identificacion == "BUL-001").EstadoBulto);
        Assert.Equal(MaquinaEstadosBulto.Devuelto, envio.Bultos.Single(b => b.Identificacion == "BUL-002").EstadoBulto);
        Assert.Equal(MaquinaEstadosBulto.Devuelto, envio.Bultos.Single(b => b.Identificacion == "BUL-003").EstadoBulto);
    }

    [Fact]
    public void EntregaParcial_SegundaVisitaFallida_PreservaEstadoEntregadoParcialmente()
    {
        var envio = CrearEnvioEnTransitoConTresBultos(out _);
        var regla = CrearReglaOperativa(admiteEntregaParcial: true, politicaEntregaParcial: ReglaOperativa.PoliticaNuevaVisitaSinRecargo);
        var motivo = new MotivoNoEntrega(Guid.NewGuid(), envio.OperadorId, "AUSENTE", "Destinatario ausente", false);

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
            "41112223",
            firmaDigitalBase64: "firma-1",
            fotoEvidenciaBase64: null,
            latitud: null,
            longitud: null,
            fechaHoraDispositivoUtc: DateTime.UtcNow);

        envio.ReintegrarADepositoPorRendicion(Guid.NewGuid());
        var ruta2Id = Guid.NewGuid();
        envio.AsignarRuta(ruta2Id, Guid.NewGuid());
        envio.DespacharEnRuta(ruta2Id, Guid.NewGuid());

        var (_, estadoFinal, _) = envio.RegistrarIntentoFallido(
            Guid.NewGuid(),
            motivo,
            regla,
            "No atendió en la segunda visita",
            null,
            null,
            null,
            DateTime.UtcNow);

        Assert.Equal(EstadoEnvio.EntregadoParcialmente, estadoFinal);
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, envio.EstadoActual);
        Assert.Equal(MaquinaEstadosBulto.Entregado, envio.Bultos.Single(b => b.Identificacion == "BUL-001").EstadoBulto);
        Assert.Equal(MaquinaEstadosBulto.PendienteReenvio, envio.Bultos.Single(b => b.Identificacion == "BUL-002").EstadoBulto);
    }

    [Fact]
    public void DespacharEnRutaParcial_ConBultoFaltante_DespachaCargadoYMarcaFaltanteEnCargaConDiscrepancia()
    {
        var envio = new Envio(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Estandar",
            "UM-CARGA-PARCIAL-01",
            Guid.NewGuid(),
            null,
            null,
            "Av. Italia 2500, Montevideo",
            "Carlos Pérez",
            OperadorId);
        envio.AgregarBulto("BUL-C1", 2.0m, 20m, 20m, 20m);
        envio.AgregarBulto("BUL-C2", 3.0m, 20m, 20m, 20m);
        var usuarioId = Guid.NewGuid();
        envio.RecepcionarEnDeposito(new[] { "BUL-C1", "BUL-C2" }, Array.Empty<string>(), null, usuarioId);
        envio.ClasificarBultos(new[]
        {
            new ClasificacionBulto("BUL-C1", PrioridadBulto.Normal),
            new ClasificacionBulto("BUL-C2", PrioridadBulto.Normal)
        }, usuarioId);
        var hojaId = Guid.NewGuid();
        envio.AsignarRuta(hojaId);

        var bultoFaltante = envio.Bultos.Single(b => b.Identificacion == "BUL-C2");
        envio.DespacharEnRutaParcial(hojaId, new[] { bultoFaltante.Id }, usuarioId, "No estaba en andén");

        Assert.Equal(EstadoEnvio.EnTransito, envio.EstadoActual);
        Assert.Equal(hojaId, envio.HojaDeRutaId);
        Assert.Equal(MaquinaEstadosBulto.CargadoEnVehiculo, envio.Bultos.Single(b => b.Identificacion == "BUL-C1").EstadoBulto);
        Assert.Equal(MaquinaEstadosBulto.FaltanteEnCarga, bultoFaltante.EstadoBulto);
        Assert.Single(envio.Discrepancias, d => d.BultoId == bultoFaltante.Id && d.TipoDiscrepancia == "FaltanteEnCarga");
    }

    [Fact]
    public void DespacharEnRutaParcial_SinBultosCargadosOConParametrosInvalidos_LanzaExcepcionDeDominio()
    {
        var envio = new Envio(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Estandar",
            "UM-CARGA-ERR-01",
            Guid.NewGuid(),
            null,
            null,
            "Bulevar Artigas 1000",
            "Sofía Silva",
            OperadorId);
        envio.AgregarBulto("BUL-E1", 2.0m, 10m, 10m, 10m);
        var usuarioId = Guid.NewGuid();
        envio.RecepcionarEnDeposito(new[] { "BUL-E1" }, Array.Empty<string>(), null, usuarioId);
        envio.ClasificarBultos(new[] { new ClasificacionBulto("BUL-E1", PrioridadBulto.Normal) }, usuarioId);
        var hojaId = Guid.NewGuid();
        envio.AsignarRuta(hojaId);

        Assert.Throws<DomainValidationException>(() =>
            envio.DespacharEnRutaParcial(Guid.Empty, Array.Empty<Guid>(), usuarioId));

        Assert.Throws<DomainValidationException>(() =>
            envio.DespacharEnRutaParcial(hojaId, new[] { envio.Bultos.Single().Id }, usuarioId));
    }

    [Fact]
    public void RegistrarEntregaParcial_ConBultoFaltanteEnCargaSinMotivoEnDomicilio_EntregaCargadosYDerivaEntregadoParcialmente()
    {
        var envio = CrearEnvioConDosBultosUnoFaltanteEnCarga(out var hojaId);
        var regla = CrearReglaOperativa(admiteEntregaParcial: true);

        var resultado = envio.RegistrarEntregaParcial(
            Guid.NewGuid(),
            regla,
            new[] { "BUL-C1" },
            Array.Empty<BultoNoEntregadoInput>(),
            "María Rodríguez",
            "41112223",
            firmaDigitalBase64: "firma-1",
            fotoEvidenciaBase64: null,
            latitud: -34.9,
            longitud: -56.1,
            fechaHoraDispositivoUtc: DateTime.UtcNow,
            Guid.NewGuid());

        Assert.Equal(EstadoEnvio.EntregadoParcialmente, resultado.EstadoFinalEnvio);
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, envio.EstadoActual);
        Assert.Equal(MaquinaEstadosBulto.Entregado, envio.Bultos.Single(b => b.Identificacion == "BUL-C1").EstadoBulto);
        Assert.Equal(MaquinaEstadosBulto.FaltanteEnCarga, envio.Bultos.Single(b => b.Identificacion == "BUL-C2").EstadoBulto);
        Assert.Equal(1, resultado.BultosEntregadosAcumulados);
        Assert.Equal(1, resultado.BultosPendientes);
        Assert.Equal(hojaId, envio.HojaDeRutaId);
    }

    [Fact]
    public void RegistrarEntregaParcial_ConFaltanteEnCargaYOperadorNoAdmiteParcial_EntregaTodosLosCargadosSinError()
    {
        var envio = CrearEnvioConDosBultosUnoFaltanteEnCarga(out _);
        var reglaSinParcialEnPuerta = CrearReglaOperativa(admiteEntregaParcial: false);

        var resultado = envio.RegistrarEntregaParcial(
            Guid.NewGuid(),
            reglaSinParcialEnPuerta,
            new[] { "BUL-C1" },
            Array.Empty<BultoNoEntregadoInput>(),
            "María Rodríguez",
            "41112223",
            firmaDigitalBase64: "firma-1",
            fotoEvidenciaBase64: null,
            latitud: null,
            longitud: null,
            fechaHoraDispositivoUtc: DateTime.UtcNow);

        Assert.Equal(EstadoEnvio.EntregadoParcialmente, resultado.EstadoFinalEnvio);
        Assert.Equal(MaquinaEstadosBulto.Entregado, envio.Bultos.Single(b => b.Identificacion == "BUL-C1").EstadoBulto);
        Assert.Equal(MaquinaEstadosBulto.FaltanteEnCarga, envio.Bultos.Single(b => b.Identificacion == "BUL-C2").EstadoBulto);
    }

    [Fact]
    public void RegistrarEntregaExitosa_ConBultoFaltanteEnCarga_RechazaExigiendoEntregaParcial()
    {
        var envio = CrearEnvioConDosBultosUnoFaltanteEnCarga(out _);
        var regla = CrearReglaOperativa();

        var ex = Assert.Throws<DomainValidationException>(() =>
            envio.RegistrarEntregaExitosa(
                Guid.NewGuid(),
                regla,
                "María Rodríguez",
                "41112223",
                firmaDigitalBase64: "firma-1",
                fotoEvidenciaBase64: null,
                latitud: null,
                longitud: null,
                fechaHoraDispositivoUtc: DateTime.UtcNow));

        Assert.Contains("entrega parcial", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(EstadoEnvio.EnTransito, envio.EstadoActual);
        Assert.Equal(MaquinaEstadosBulto.FaltanteEnCarga, envio.Bultos.Single(b => b.Identificacion == "BUL-C2").EstadoBulto);
    }

    [Fact]
    public void RegistrarEntregaParcial_TresBultosUnoFaltanteEnCargaYOtroPerdidoEnEntrega_RegistraAmbos()
    {
        var envio = new Envio(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Estandar",
            "UM-COMBINADO-01",
            Guid.NewGuid(),
            null,
            null,
            "Av. Rivera 2100",
            "Lucía Gómez",
            OperadorId);
        envio.AgregarBulto("BUL-1", 1.5m, 15m, 15m, 15m);
        envio.AgregarBulto("BUL-2", 1.5m, 15m, 15m, 15m);
        envio.AgregarBulto("BUL-3", 1.5m, 15m, 15m, 15m);
        var usuarioId = Guid.NewGuid();
        envio.RecepcionarEnDeposito(new[] { "BUL-1", "BUL-2", "BUL-3" }, Array.Empty<string>(), null, usuarioId);
        envio.ClasificarBultos(new[]
        {
            new ClasificacionBulto("BUL-1", PrioridadBulto.Normal),
            new ClasificacionBulto("BUL-2", PrioridadBulto.Normal),
            new ClasificacionBulto("BUL-3", PrioridadBulto.Normal)
        }, usuarioId);
        var hojaId = Guid.NewGuid();
        envio.AsignarRuta(hojaId);
        envio.DespacharEnRutaParcial(hojaId, new[] { envio.Bultos.Single(b => b.Identificacion == "BUL-3").Id }, usuarioId, "Faltante en carga");

        var regla = CrearReglaOperativa();
        var motivo = new MotivoNoEntrega(Guid.NewGuid(), OperadorId, "EXTR_REPARTO", "Extraviado durante el reparto", true);

        var resultado = envio.RegistrarEntregaParcial(
            Guid.NewGuid(),
            regla,
            new[] { "BUL-1" },
            new[] { new BultoNoEntregadoInput("BUL-2", motivo, "No se ubicó en el furgón") },
            "Lucía Gómez",
            "42223334",
            firmaDigitalBase64: "firma-ok",
            fotoEvidenciaBase64: null,
            latitud: null,
            longitud: null,
            fechaHoraDispositivoUtc: DateTime.UtcNow,
            usuarioId);

        Assert.Equal(EstadoEnvio.EntregadoParcialmente, resultado.EstadoFinalEnvio);
        Assert.Equal(MaquinaEstadosBulto.Entregado, envio.Bultos.Single(b => b.Identificacion == "BUL-1").EstadoBulto);
        Assert.Equal(MaquinaEstadosBulto.PendienteReenvio, envio.Bultos.Single(b => b.Identificacion == "BUL-2").EstadoBulto);
        Assert.Equal(MaquinaEstadosBulto.FaltanteEnCarga, envio.Bultos.Single(b => b.Identificacion == "BUL-3").EstadoBulto);
        Assert.Equal(1, resultado.BultosEntregadosAcumulados);
        Assert.Equal(2, resultado.BultosPendientes);
    }

    private static Envio CrearEnvioConDosBultosUnoFaltanteEnCarga(out Guid hojaDeRutaId)
    {
        var envio = new Envio(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Estandar",
            "UM-CARGA-DOS-01",
            Guid.NewGuid(),
            null,
            null,
            "Av. 18 de Julio 1234, Montevideo",
            "María Rodríguez",
            OperadorId);
        envio.AgregarBulto("BUL-C1", 2.0m, 20m, 20m, 20m);
        envio.AgregarBulto("BUL-C2", 2.5m, 20m, 20m, 20m);
        var usuarioId = Guid.NewGuid();
        envio.RecepcionarEnDeposito(new[] { "BUL-C1", "BUL-C2" }, Array.Empty<string>(), null, usuarioId);
        envio.ClasificarBultos(new[]
        {
            new ClasificacionBulto("BUL-C1", PrioridadBulto.Normal),
            new ClasificacionBulto("BUL-C2", PrioridadBulto.Normal)
        }, usuarioId);
        hojaDeRutaId = Guid.NewGuid();
        envio.AsignarRuta(hojaDeRutaId);
        var faltanteId = envio.Bultos.Single(b => b.Identificacion == "BUL-C2").Id;
        envio.DespacharEnRutaParcial(hojaDeRutaId, new[] { faltanteId }, usuarioId, "Faltante en andén");
        return envio;
    }
}

