using UltimaMilla.Application.Commands.Indicadores.EjecutarCierreDiario;
using UltimaMilla.Application.Commands.Indicadores.RecalcularIndicadoresCumplimiento;
using UltimaMilla.Application.Commands.Mensajeria;
using UltimaMilla.Application.Commands.Mensajeria.ProcesarMensaje;
using UltimaMilla.Application.Commands.Mensajeria.PublicarOutboxPendiente;
using UltimaMilla.Application.Commands.Mensajeria.RegistrarMensajeFallido;
using UltimaMilla.Application.Commands.Mensajeria.ReintentarAvisosPendientes;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Incidencias;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;
using Xunit;

namespace UltimaMilla.Application.Tests;

public class WorkerHandlersTests
{
    private static readonly Guid OperadorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ComercioOperadorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateOnly Viernes = new(2026, 10, 16);

    private readonly FakeOutboxRepository _outboxRepo = new();
    private readonly FakeMessagePublisher _publisher = new();
    private readonly FakeIdempotenciaRepository _idempotenciaRepo = new();
    private readonly FakeMensajeFallidoRepository _fallidosRepo = new();
    private readonly FakeNotificacionRepository _notificacionesRepo = new();
    private readonly FakeNotificacionDestinatarioService _notificacionService = new();
    private readonly FakeAvisoComercioRepository _avisosRepo = new();
    private readonly FakeWebhookSender _webhookSender = new();
    private readonly FakeEnvioRepository _enviosRepo = new();
    private readonly FakeDestinatarioRepository _destinatariosRepo = new();
    private readonly FakeFranjaHorariaRepository _franjasRepo = new();
    private readonly FakeRutaRepository _rutasRepo = new();
    private readonly FakeIndicadorCumplimientoRepository _indicadoresRepo = new();
    private readonly FakeCierreDiarioRepository _cierresRepo = new();
    private readonly FakeProcesosProgramadosRepository _procesosRepo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeClock _clock = new() { UtcNow = new DateTime(2026, 10, 16, 18, 0, 0, DateTimeKind.Utc) };

    private PublicarOutboxPendienteCommandHandler CrearPublicadorHandler() =>
        new(_outboxRepo, _publisher, _uow, _clock);

    private AvisoComercioEntregador CrearEntregador() =>
        new(_avisosRepo, _webhookSender, _uow, _clock);

    private ProcesarMensajeCommandHandler CrearConsumidorHandler() =>
        new(_idempotenciaRepo, _enviosRepo, _destinatariosRepo, _notificacionesRepo, _notificacionService, _avisosRepo, CrearEntregador(), _uow);

    private ReintentarAvisosPendientesCommandHandler CrearReintentarAvisosHandler() =>
        new(_avisosRepo, CrearEntregador(), _clock);

    private RegistrarMensajeFallidoCommandHandler CrearRegistrarFallidoHandler() =>
        new(_fallidosRepo, _uow);

    private RecalcularIndicadoresCumplimientoCommandHandler CrearIndicadoresHandler() =>
        new(_procesosRepo, _franjasRepo, _rutasRepo, _indicadoresRepo, _uow, _clock);

    private EjecutarCierreDiarioCommandHandler CrearCierreHandler() =>
        new(_cierresRepo, _procesosRepo, _rutasRepo, _uow, _clock);

    private (Envio Envio, Destinatario Destinatario) CrearEnvioConDestinatario(
        string? email = "cliente@ejemplo.com",
        string? telefono = "099111222",
        Guid? franjaId = null,
        Guid? operadorId = null)
    {
        var destinatario = new Destinatario(Guid.NewGuid(), "Ana Gómez", "4.123.456-7", email, telefono);
        _destinatariosRepo.Destinatarios.Add(destinatario);

        var envio = new Envio(
            Guid.NewGuid(),
            ComercioOperadorId,
            destinatario.Id,
            "Estandar",
            $"ENV-{Guid.NewGuid():N}"[..16],
            direccionEntregaId: Guid.NewGuid(),
            franjaHorariaComprometidaId: franjaId,
            loteImportacionId: null,
            direccionDestino: "Av. 18 de Julio 1234",
            nombreDestinatario: destinatario.Nombre,
            operadorId: operadorId ?? OperadorId);

        envio.AgregarBulto("BUL-01", 2m);
        _enviosRepo.Envios.Add(envio);
        return (envio, destinatario);
    }

    // -------------------------------------------------------------------------
    // Publicador de Outbox (§6.8)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PublicarOutbox_RequestNuloOLoteInvalido_LanzaExcepcion()
    {
        var handler = CrearPublicadorHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(new PublicarOutboxPendienteCommand(0), CancellationToken.None));
        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(new PublicarOutboxPendienteCommand(-5), CancellationToken.None));
    }

    [Fact]
    public async Task PublicarOutbox_PublicaEnOrdenYDetieneSinPerderMensajesAnteFalloDelBroker()
    {
        var m1 = new OutboxMessage(Guid.NewGuid(), "Envio", Guid.NewGuid(), EventoEnvioPublicado.TipoMensaje, "{\"correlationId\":\"corr-1\"}");
        var m2 = new OutboxMessage(Guid.NewGuid(), "Envio", Guid.NewGuid(), EventoEnvioPublicado.TipoMensaje, "{\"correlationId\":\"corr-2\"}");
        _outboxRepo.Mensajes.AddRange(new[] { m1, m2 });

        _publisher.FallarEnIndice = 1; // falla en el segundo mensaje
        var handler = CrearPublicadorHandler();

        var parcial = await handler.Handle(new PublicarOutboxPendienteCommand(10), CancellationToken.None);

        Assert.Equal(1, parcial.Publicados);
        Assert.Equal(1, parcial.Pendientes);
        Assert.NotNull(parcial.Error);
        Assert.True(m1.Procesado);
        Assert.False(m2.Procesado);

        // Cuando el broker se recupera, el mensaje restante se publica
        _publisher.FallarEnIndice = null;
        var recuperacion = await handler.Handle(new PublicarOutboxPendienteCommand(10), CancellationToken.None);

        Assert.Equal(1, recuperacion.Publicados);
        Assert.Equal(0, recuperacion.Pendientes);
        Assert.Null(recuperacion.Error);
        Assert.True(m2.Procesado);
        Assert.Equal("corr-2", _publisher.Publicados.Last().CorrelationId);
    }

    // -------------------------------------------------------------------------
    // Consumidor idempotente, UC-35 (Notificaciones) y UC-36 (Webhooks)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcesarMensaje_EntradasInvalidas_LanzaExcepcionTipada()
    {
        var handler = CrearConsumidorHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(new ProcesarMensajeCommand(Guid.Empty, "Envio", Guid.NewGuid(), "Tipo", "{}"), CancellationToken.None));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(new ProcesarMensajeCommand(Guid.NewGuid(), "Envio", Guid.NewGuid(), "  ", "{}"), CancellationToken.None));
        await Assert.ThrowsAsync<MensajeInvalidoException>(() =>
            handler.Handle(new ProcesarMensajeCommand(Guid.NewGuid(), "Envio", Guid.NewGuid(), EventoEnvioPublicado.TipoMensaje, "{malformado"), CancellationToken.None));
    }

    [Fact]
    public async Task ProcesarMensaje_Idempotencia_NoDuplicaEfectosAlReprocesarMismoMessageId()
    {
        var (envio, _) = CrearEnvioConDestinatario();
        var suscripcion = new SuscripcionAviso(Guid.NewGuid(), ComercioOperadorId, "EnTransito", "https://comercio.test/webhook", "clave-secreta-123");
        _avisosRepo.Suscripciones.Add(suscripcion);

        var evento = new EventoEnvioPublicado(
            OperadorId, envio.Id, ComercioOperadorId, envio.CodigoSeguimientoPublico, "Despacho", "AsignadoARuta", "EnTransito", _clock.UtcNow, "corr-abc");
        var comando = new ProcesarMensajeCommand(Guid.NewGuid(), "Envio", envio.Id, EventoEnvioPublicado.TipoMensaje, evento.Serializar());
        var handler = CrearConsumidorHandler();

        var primera = await handler.Handle(comando, CancellationToken.None);
        var segunda = await handler.Handle(comando, CancellationToken.None);

        Assert.Equal(EstadoProcesamientoMensaje.Procesado, primera.Estado);
        Assert.Equal(1, primera.NotificacionesEnviadas);
        Assert.Equal(1, primera.AvisosGenerados);

        Assert.Equal(EstadoProcesamientoMensaje.Duplicado, segunda.Estado);
        Assert.Single(_notificacionesRepo.Notificaciones);
        Assert.Single(_avisosRepo.Avisos);
        Assert.Single(_webhookSender.Llamadas);

        // Verifica que la firma HMAC-SHA256 enviada al comercio sea válida
        var llamada = _webhookSender.Llamadas.Single();
        Assert.True(FirmaWebhook.EsValida("clave-secreta-123", llamada.Payload, llamada.Firma));
        Assert.Equal("Entregado", _avisosRepo.Avisos.Single().Estado);
    }

    [Fact]
    public async Task ProcesarMensaje_EnvioDeOtroOperador_RechazaPorAislamientoMultitenant()
    {
        var otroOperador = Guid.NewGuid();
        var (envio, _) = CrearEnvioConDestinatario(operadorId: otroOperador);
        var evento = new EventoEnvioPublicado(
            OperadorId, envio.Id, ComercioOperadorId, envio.CodigoSeguimientoPublico, "Entrega", "EnTransito", "Entregado", _clock.UtcNow, "corr-1");

        var handler = CrearConsumidorHandler();

        await Assert.ThrowsAsync<MensajeInvalidoException>(() =>
            handler.Handle(new ProcesarMensajeCommand(Guid.NewGuid(), "Envio", envio.Id, EventoEnvioPublicado.TipoMensaje, evento.Serializar()), CancellationToken.None));
        Assert.Empty(_idempotenciaRepo.Claves);
    }

    [Fact]
    public async Task ProcesarMensaje_TipoSinManejadorDesdeColaFallidos_LanzaMensajeSinManejadorException()
    {
        var handler = CrearConsumidorHandler();

        var normal = await handler.Handle(
            new ProcesarMensajeCommand(Guid.NewGuid(), "Repartidor", Guid.NewGuid(), "EventoInformativo", "{}"), CancellationToken.None);
        Assert.Equal(EstadoProcesamientoMensaje.SinConsumidor, normal.Estado);

        await Assert.ThrowsAsync<MensajeSinManejadorException>(() =>
            handler.Handle(new ProcesarMensajeCommand(Guid.NewGuid(), EventoEnvioPublicado.OrigenColaFallidos, Guid.NewGuid(), "GeneracionEtiquetaZPL", "{}"), CancellationToken.None));
    }

    [Fact]
    public async Task AvisosComercio_FalloYReintentosCrecientesHastaAgotar_TransicionaAReintentandoYAgotado()
    {
        var (envio, _) = CrearEnvioConDestinatario(email: null, telefono: "099888777");
        var suscripcion = new SuscripcionAviso(Guid.NewGuid(), ComercioOperadorId, "*", "https://comercio.test/hook", "secreto");
        _avisosRepo.Suscripciones.Add(suscripcion);
        _webhookSender.Respuesta = new ResultadoWebhook(false, 503, "HTTP 503 Service Unavailable");

        var evento = new EventoEnvioPublicado(
            OperadorId, envio.Id, ComercioOperadorId, envio.CodigoSeguimientoPublico, "Entrega", "EnTransito", "Entregado", _clock.UtcNow, "corr-2");
        var consumidor = CrearConsumidorHandler();
        var reintentador = CrearReintentarAvisosHandler();

        // Intento 1 (al consumir el evento) -> queda en Reintentando a +1 min, y notifica por SMS como fallback sin email
        await consumidor.Handle(new ProcesarMensajeCommand(Guid.NewGuid(), "Envio", envio.Id, EventoEnvioPublicado.TipoMensaje, evento.Serializar()), CancellationToken.None);
        var aviso = _avisosRepo.Avisos.Single();
        Assert.Equal("SMS", _notificacionesRepo.Notificaciones.Single().Canal);
        Assert.Equal("Reintentando", aviso.Estado);
        Assert.Equal(1, aviso.Intentos);
        Assert.Equal(_clock.UtcNow.AddMinutes(1), aviso.ProximoReintento);

        // Intentos 2 (+5 min) y 3 (+15 min) siguen en Reintentando; intento 4 agota la política y pasa a Agotado
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        await reintentador.Handle(new ReintentarAvisosPendientesCommand(10), CancellationToken.None);
        Assert.Equal("Reintentando", aviso.Estado);
        Assert.Equal(2, aviso.Intentos);

        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
        await reintentador.Handle(new ReintentarAvisosPendientesCommand(10), CancellationToken.None);
        Assert.Equal("Reintentando", aviso.Estado);
        Assert.Equal(3, aviso.Intentos);

        _clock.UtcNow = _clock.UtcNow.AddMinutes(15);
        var final = await reintentador.Handle(new ReintentarAvisosPendientesCommand(10), CancellationToken.None);
        Assert.Equal(1, final.Agotados);
        Assert.Equal("Agotado", aviso.Estado);
        Assert.Equal(4, aviso.Intentos);
        Assert.Null(aviso.ProximoReintento);
    }

    // -------------------------------------------------------------------------
    // Cola de mensajes fallidos (DLQ y UC-42)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RegistrarMensajeFallido_NuevoYReintentoDesdeColaFallidos_RegistraYActualizaSinDuplicar()
    {
        var handler = CrearRegistrarFallidoHandler();
        var payload = $"{{\"operadorId\":\"{OperadorId}\"}}";

        var id = await handler.Handle(
            new RegistrarMensajeFallidoCommand(Guid.NewGuid(), "Envio", Guid.NewGuid(), "EnvioCambioEstado", payload, "Timeout", 4, "cola.worker"),
            CancellationToken.None);

        var guardado = _fallidosRepo.Mensajes.Single();
        Assert.Equal(id, guardado.Id);
        Assert.Equal(4, guardado.Intentos);
        Assert.Equal(OperadorId, guardado.OperadorId);

        // Si un reintento manual desde ColaFallidos vuelve a fallar, incrementa el existente en vez de crear otro
        var idReintento = await handler.Handle(
            new RegistrarMensajeFallidoCommand(Guid.NewGuid(), EventoEnvioPublicado.OrigenColaFallidos, id, "EnvioCambioEstado", payload, "Nuevo fallo", 4, "cola.worker"),
            CancellationToken.None);

        Assert.Equal(id, idReintento);
        Assert.Single(_fallidosRepo.Mensajes);
        Assert.Equal(5, guardado.Intentos);
    }

    // -------------------------------------------------------------------------
    // UC-37 (Indicadores de cumplimiento) y UC-38 (Cierre diario)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RecalcularIndicadoresYCierreDiario_ValidacionesYCasoBordeSinEnvios_NoDividePorCeroYEsIdempotente()
    {
        var indicadoresHandler = CrearIndicadoresHandler();
        var cierreHandler = CrearCierreHandler();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            indicadoresHandler.Handle(new RecalcularIndicadoresCumplimientoCommand(Guid.Empty, Viernes), CancellationToken.None));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            indicadoresHandler.Handle(new RecalcularIndicadoresCumplimientoCommand(OperadorId, Viernes.AddDays(2)), CancellationToken.None));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            cierreHandler.Handle(new EjecutarCierreDiarioCommand(Guid.Empty, Viernes), CancellationToken.None));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            cierreHandler.Handle(new EjecutarCierreDiarioCommand(OperadorId, Viernes.AddDays(2)), CancellationToken.None));

        // Operador con 0 envíos: genera indicador general con 0% sin fallar
        var vacio = await indicadoresHandler.Handle(new RecalcularIndicadoresCumplimientoCommand(OperadorId, Viernes), CancellationToken.None);
        Assert.Equal(0, vacio.TotalEnvios);
        Assert.Single(_indicadoresRepo.Indicadores);
        Assert.Equal(0m, _indicadoresRepo.Indicadores.Single().PorcentajeCumplimiento);

        // Cierre diario idempotente: la segunda ejecución devuelve el mismo cierre sin duplicar
        var cierre1 = await cierreHandler.Handle(new EjecutarCierreDiarioCommand(OperadorId, Viernes), CancellationToken.None);
        var cierre2 = await cierreHandler.Handle(new EjecutarCierreDiarioCommand(OperadorId, Viernes), CancellationToken.None);
        Assert.False(cierre1.YaExistia);
        Assert.True(cierre2.YaExistia);
        Assert.Equal(cierre1.CierreId, cierre2.CierreId);
        Assert.Single(_cierresRepo.Cierres);
    }

    [Fact]
    public async Task RecalcularIndicadores_ClasificaEnPlazoFueraDePlazoYNoEntregadoYActualizaEnReejecucion()
    {
        var zonaId = Guid.NewGuid();
        var franja = new FranjaHoraria(Guid.NewGuid(), zonaId, DayOfWeek.Friday, new TimeOnly(8, 0), new TimeOnly(12, 0));
        _franjasRepo.Franjas.Add(franja);

        var (enPlazo, _) = CrearEnvioConDestinatario(franjaId: franja.Id);
        var (fueraDePlazo, _) = CrearEnvioConDestinatario(franjaId: franja.Id);
        var (noEntregado, _) = CrearEnvioConDestinatario(franjaId: franja.Id);

        // Viernes 16/10: 14:00 UTC = 11:00 UY (en plazo <= 12:00); 17:00 UTC = 14:00 UY (fuera de plazo > 12:00)
        AgregarEventoResultado(enPlazo, EstadoEnvio.Entregado, new DateTime(2026, 10, 16, 14, 0, 0, DateTimeKind.Utc));
        AgregarEventoResultado(fueraDePlazo, EstadoEnvio.Entregado, new DateTime(2026, 10, 16, 17, 0, 0, DateTimeKind.Utc));
        AgregarEventoResultado(noEntregado, EstadoEnvio.NoEntregado, new DateTime(2026, 10, 16, 16, 0, 0, DateTimeKind.Utc));
        _procesosRepo.Envios.AddRange(new[] { enPlazo, fueraDePlazo, noEntregado });

        var handler = CrearIndicadoresHandler();
        var resultado = await handler.Handle(new RecalcularIndicadoresCumplimientoCommand(OperadorId, Viernes), CancellationToken.None);

        Assert.Equal(3, resultado.TotalEnvios);
        var general = _indicadoresRepo.Indicadores.Single(i => i.ZonaCoberturaId is null && i.RepartidorId is null && i.ComercioOperadorId is null);
        Assert.Equal(3, general.TotalEnvios);
        Assert.Equal(1, general.EntregadosEnPlazo);
        Assert.Equal(1, general.EntregadosFueraDePlazo);
        Assert.Equal(1, general.NoEntregados);

        // Reejecución actualiza los indicadores existentes en lugar de duplicarlos
        var reejecucion = await handler.Handle(new RecalcularIndicadoresCumplimientoCommand(OperadorId, Viernes), CancellationToken.None);
        Assert.Equal(0, reejecucion.IndicadoresGenerados);
        Assert.True(reejecucion.IndicadoresActualizados >= 2);
    }

    private static void AgregarEventoResultado(Envio envio, EstadoEnvio estado, DateTime timestampUtc)
    {
        var evento = new EventoEnvio(Guid.NewGuid(), envio.Id, "Resultado", EstadoEnvio.EnTransito, estado, "Test");
        typeof(EventoEnvio).GetProperty(nameof(EventoEnvio.Timestamp))!.SetValue(evento, timestampUtc);
        var lista = (List<EventoEnvio>)typeof(Envio).GetField("_eventos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(envio)!;
        lista.Add(evento);
    }

    // -------------------------------------------------------------------------
    // Fakes de soporte para los puertos del Worker
    // -------------------------------------------------------------------------

    private sealed class FakeOutboxRepository : IOutboxRepository
    {
        public List<OutboxMessage> Mensajes { get; } = new();

        public Task<List<OutboxMessage>> ListarPendientesAsync(int maximo, CancellationToken cancellationToken = default) =>
            Task.FromResult(Mensajes.Where(m => !m.Procesado).OrderBy(m => m.CreadoEn).Take(maximo).ToList());
    }

    private sealed class FakeMessagePublisher : IMessagePublisher
    {
        public List<MensajeSaliente> Publicados { get; } = new();
        public int? FallarEnIndice { get; set; }

        public Task PublicarAsync(MensajeSaliente mensaje, CancellationToken cancellationToken = default)
        {
            if (FallarEnIndice.HasValue && Publicados.Count == FallarEnIndice.Value)
                throw new InvalidOperationException("Conexión rechazada por RabbitMQ.");

            Publicados.Add(mensaje);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeIdempotenciaRepository : IIdempotenciaConsumidorRepository
    {
        public List<ClaveIdempotenciaConsumidor> Claves { get; } = new();

        public Task<bool> ExisteAsync(string mensajeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Claves.Any(c => c.MensajeId == mensajeId));

        public Task AddAsync(ClaveIdempotenciaConsumidor clave, CancellationToken cancellationToken = default)
        {
            Claves.Add(clave);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMensajeFallidoRepository : IMensajeFallidoRepository
    {
        public List<MensajeFallidoCola> Mensajes { get; } = new();

        public Task<MensajeFallidoCola?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Mensajes.FirstOrDefault(m => m.Id == id));

        public Task AddAsync(MensajeFallidoCola mensaje, CancellationToken cancellationToken = default)
        {
            Mensajes.Add(mensaje);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotificacionRepository : INotificacionRepository
    {
        public List<Notificacion> Notificaciones { get; } = new();

        public Task AddAsync(Notificacion notificacion, CancellationToken cancellationToken = default)
        {
            Notificaciones.Add(notificacion);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotificacionDestinatarioService : INotificacionDestinatarioService
    {
        public List<NotificacionDestinatario> Enviadas { get; } = new();

        public Task EnviarAsync(NotificacionDestinatario notificacion, CancellationToken cancellationToken = default)
        {
            Enviadas.Add(notificacion);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAvisoComercioRepository : IAvisoComercioRepository
    {
        public List<SuscripcionAviso> Suscripciones { get; } = new();
        public List<AvisoComercio> Avisos { get; } = new();

        public Task<List<SuscripcionAviso>> ListarSuscripcionesActivasAsync(Guid comercioOperadorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Suscripciones.Where(s => s.ComercioOperadorId == comercioOperadorId && s.Activa).ToList());

        public Task<SuscripcionAviso?> GetSuscripcionAsync(Guid suscripcionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Suscripciones.FirstOrDefault(s => s.Id == suscripcionId));

        public Task AddAsync(AvisoComercio aviso, CancellationToken cancellationToken = default)
        {
            Avisos.Add(aviso);
            return Task.CompletedTask;
        }

        public Task<List<AvisoComercio>> ListarPendientesDeEntregaAsync(DateTime ahoraUtc, int maximo, CancellationToken cancellationToken = default) =>
            Task.FromResult(Avisos
                .Where(a => a.Estado is "Pendiente" or "Reintentando" && (a.ProximoReintento == null || a.ProximoReintento <= ahoraUtc))
                .Take(maximo)
                .ToList());
    }

    private sealed class FakeWebhookSender : IWebhookSender
    {
        public List<(string Url, string Payload, string Firma, Guid AvisoId)> Llamadas { get; } = new();
        public ResultadoWebhook Respuesta { get; set; } = new(true, 200, "HTTP 200 OK");

        public Task<ResultadoWebhook> EnviarAsync(string urlWebhook, string payload, string firma, Guid avisoId, CancellationToken cancellationToken = default)
        {
            Llamadas.Add((urlWebhook, payload, firma, avisoId));
            return Task.FromResult(Respuesta);
        }
    }

    private sealed class FakeIndicadorCumplimientoRepository : IIndicadorCumplimientoRepository
    {
        public List<IndicadorCumplimiento> Indicadores { get; } = new();

        public Task<List<IndicadorCumplimiento>> ListarPorPeriodoAsync(Guid operadorId, string periodo, CancellationToken cancellationToken = default) =>
            Task.FromResult(Indicadores.Where(i => i.OperadorId == operadorId && i.Periodo == periodo).ToList());

        public Task AddAsync(IndicadorCumplimiento indicador, CancellationToken cancellationToken = default)
        {
            Indicadores.Add(indicador);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCierreDiarioRepository : ICierreDiarioRepository
    {
        public List<CierreDiarioOperacion> Cierres { get; } = new();

        public Task<CierreDiarioOperacion?> GetAsync(Guid operadorId, DateOnly fecha, CancellationToken cancellationToken = default) =>
            Task.FromResult(Cierres.FirstOrDefault(c => c.OperadorId == operadorId && c.Fecha == fecha));

        public Task AddAsync(CierreDiarioOperacion cierre, CancellationToken cancellationToken = default)
        {
            Cierres.Add(cierre);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProcesosProgramadosRepository : IProcesosProgramadosRepository
    {
        public List<Envio> Envios { get; } = new();

        public Task<List<Guid>> ListarOperadoresActivosAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Guid> { OperadorId });

        public Task<List<Envio>> ListarEnviosConResultadoAsync(Guid operadorId, DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken = default) =>
            Task.FromResult(Envios.Where(e => e.OperadorId == operadorId).ToList());

        public Task<int> ContarEnviosAdmitidosAsync(Guid operadorId, DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken = default) =>
            Task.FromResult(Envios.Count(e => e.OperadorId == operadorId));

        public Task<(int Abiertas, int SinCerrar)> ContarIncidenciasAsync(Guid operadorId, DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken = default) =>
            Task.FromResult((0, 0));
    }
}
