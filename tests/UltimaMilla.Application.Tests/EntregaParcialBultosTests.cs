using System.Text.Json;
using UltimaMilla.Application.Commands.Mensajeria;
using UltimaMilla.Application.Commands.Mensajeria.ProcesarMensaje;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarEntregaParcial;
using UltimaMilla.Application.DTOs.Mobile;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Planificacion;
using UltimaMilla.Application.Queries.Seguimiento.ConsultarSeguimientoPublico;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class EntregaParcialBultosTests
{
    private static readonly Guid OperadorId = Guid.NewGuid();
    private static readonly Guid ComercioOperadorId = Guid.NewGuid();
    private static readonly Guid RepartidorId = Guid.NewGuid();
    private static readonly Guid UsuarioRepartidorId = Guid.NewGuid();

    private static (Envio Envio, HojaDeRuta Ruta, Parada Parada, MotivoNoEntrega Motivo, ReglaOperativa Regla) CrearEscenarioEnTransito(
        bool admiteEntregaParcial = true,
        string politicaEntregaParcial = ReglaOperativa.PoliticaNuevaVisitaSinRecargo)
    {
        var direccionId = Guid.NewGuid();
        var envio = new Envio(
            Guid.NewGuid(),
            ComercioOperadorId,
            Guid.NewGuid(),
            "Estandar",
            "UM-PARCIAL-APP-01",
            direccionEntregaId: direccionId,
            direccionDestino: "Av. 18 de Julio 1234",
            nombreDestinatario: "Lucia Gomez",
            operadorId: OperadorId);

        envio.AgregarBulto("BLT-A1", 2m);
        envio.AgregarBulto("BLT-A2", 3m);
        envio.AgregarBulto("BLT-A3", 4m);
        envio.RecepcionarEnDeposito(new[] { "BLT-A1", "BLT-A2", "BLT-A3" }, null, null, Guid.NewGuid());
        var zonaId = Guid.NewGuid();
        foreach (var bulto in envio.Bultos)
            bulto.Clasificar(PrioridadBulto.Normal, zonaId, "General");

        var paradaId = Guid.NewGuid();
        var ruta = new HojaDeRuta(Guid.NewGuid(), OperadorId, DateOnly.FromDateTime(DateTime.UtcNow), "EnTransito");
        ruta.AsignarRecursos(RepartidorId, Guid.NewGuid());
        ruta.AgregarParada(paradaId, envio.Id, 1, direccionId);
        var parada = ruta.Paradas.Single();

        envio.AsignarRuta(ruta.Id);
        foreach (var bulto in envio.Bultos)
            bulto.MarcarCargadoEnVehiculo();

        envio.DespacharEnRuta(ruta.Id, UsuarioRepartidorId);

        var motivo = new MotivoNoEntrega(Guid.NewGuid(), OperadorId, "RECHAZO_PARCIAL", "Destinatario rechaza bulto", true);
        var regla = new ReglaOperativa(
            Guid.NewGuid(),
            OperadorId,
            1,
            DateTime.UtcNow.AddDays(-1),
            maxIntentosEntrega: 2,
            plazoEntreIntentos: TimeSpan.FromHours(24),
            tipoPruebaEntregaExigida: "Firma",
            politicaDevolucion: "Retorno a depósito",
            plazoComprometidoEstandar: TimeSpan.FromHours(48),
            plazoComprometidoUrgente: TimeSpan.FromHours(24),
            admiteEntregaParcial: admiteEntregaParcial,
            politicaEntregaParcial: politicaEntregaParcial);

        return (envio, ruta, parada, motivo, regla);
    }

    [Fact]
    public async Task RegistrarEntregaParcialHandler_Entrega2De3_PublicaOutboxConBultosYActualizaParada()
    {
        var (envio, ruta, parada, motivo, regla) = CrearEscenarioEnTransito();
        var envioRepo = new FakeEnvioParcialRepo(envio, regla, motivo);
        var rutaRepo = new FakeRutaParcialRepo(ruta);
        var outbox = new FakeOutboxParcialStorage();
        var cache = new FakeCacheParcialService();
        var tenant = new FakeTenantParcial(OperadorId, UsuarioRepartidorId);

        var handler = new RegistrarEntregaParcialCommandHandler(
            envioRepo,
            rutaRepo,
            new FakeUnitOfWorkParcial(),
            outbox,
            cache,
            tenant,
            new FakeClockParcial());

        var cmd = new RegistrarEntregaParcialCommand(
            ruta.Id,
            parada.Id,
            envio.Id,
            "IDEMP-PARCIAL-HANDLER-01",
            new[] { "BLT-A1", "BLT-A2" },
            new[] { new BultoNoEntregadoRequestDto("BLT-A3", "RECHAZO_PARCIAL", null, "Caja abollada") },
            "Lucia Gomez",
            "41234567",
            "BASE64FIRMA",
            "BASE64FOTO",
            -34.9011,
            -56.1645);

        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(res.Idempotente);
        Assert.Equal("EntregadoParcialmente", res.EstadoEnvio);
        Assert.Equal("EntregadaParcialmente", res.EstadoParada);
        Assert.Equal(3, res.TotalBultos);
        Assert.Equal(2, res.BultosEntregados);
        Assert.Equal(1, res.BultosPendientes);
        Assert.Single(outbox.Mensajes);

        using var docOutbox = JsonDocument.Parse(outbox.Mensajes[0].Payload);
        Assert.Equal("EnvioEntregadoParcialmenteIntegrationEvent", outbox.Mensajes[0].TipoEvento);
        Assert.Equal("EntregadoParcialmente", docOutbox.RootElement.GetProperty("Estado").GetString());
        Assert.Equal(2, docOutbox.RootElement.GetProperty("CodigosEntregados").GetArrayLength());
        Assert.Equal(1, docOutbox.RootElement.GetProperty("CodigosNoEntregados").GetArrayLength());

        // Reintento offline con la misma clave de idempotencia no duplica mensajes outbox
        var resDuplicado = await handler.Handle(cmd, CancellationToken.None);
        Assert.True(resDuplicado.Idempotente);
        Assert.Single(outbox.Mensajes);
    }

    [Fact]
    public async Task RegistrarEntregaParcialHandler_OperadorNoAdmiteEntregaParcial_RechazaConDomainValidationException()
    {
        var (envio, ruta, parada, motivo, regla) = CrearEscenarioEnTransito(admiteEntregaParcial: false);
        var handler = new RegistrarEntregaParcialCommandHandler(
            new FakeEnvioParcialRepo(envio, regla, motivo),
            new FakeRutaParcialRepo(ruta),
            new FakeUnitOfWorkParcial(),
            new FakeOutboxParcialStorage(),
            new FakeCacheParcialService(),
            new FakeTenantParcial(OperadorId, UsuarioRepartidorId),
            new FakeClockParcial());

        var cmd = new RegistrarEntregaParcialCommand(
            ruta.Id,
            parada.Id,
            envio.Id,
            "IDEMP-NO-ADMITE",
            new[] { "BLT-A1" },
            new[]
            {
                new BultoNoEntregadoRequestDto("BLT-A2", "RECHAZO_PARCIAL"),
                new BultoNoEntregadoRequestDto("BLT-A3", "RECHAZO_PARCIAL")
            },
            "Lucia Gomez",
            "41234567",
            "BASE64FIRMA",
            null,
            -34.9011,
            -56.1645);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task SeguimientoPublico_ConEntregaParcial_MuestraAvanceDosDeTresSinExponerCodigosDeBulto()
    {
        var (envio, _, _, motivo, regla) = CrearEscenarioEnTransito();
        envio.RegistrarEntregaParcial(
            UsuarioRepartidorId,
            regla,
            new[] { "BLT-A1", "BLT-A2" },
            new[] { new BultoNoEntregadoInput("BLT-A3", motivo) },
            "Lucia Gomez",
            "41234567",
            "BASE64FIRMA",
            null,
            -34.9011,
            -56.1645,
            DateTime.UtcNow,
            UsuarioRepartidorId);

        var queryHandler = new ConsultarSeguimientoPublicoQueryHandler(
            new FakeEnvioParcialRepo(envio, regla, motivo),
            new FakeFranjaHorariaRepository(),
            new FakeCacheParcialService());

        var dto = await queryHandler.Handle(new ConsultarSeguimientoPublicoQuery("UM-PARCIAL-APP-01"), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, dto!.EstadoActual);
        Assert.Equal(3, dto.TotalBultos);
        Assert.Equal(2, dto.BultosEntregados);
        Assert.Equal(1, dto.BultosPendientes);
        Assert.Equal("2 de 3 bultos entregados", dto.AvanceEntregaBultos);

        // RF-35: verificar que el JSON serializado del seguimiento público no expone identificadores internos de bultos
        var json = JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("BLT-A1", json);
        Assert.DoesNotContain("BLT-A2", json);
        Assert.DoesNotContain("BLT-A3", json);
    }

    [Fact]
    public async Task ListarEnviosPendientesDeRuta_IncluyeEnvioEntregadoParcialmenteConBultosPendientesDisponibles()
    {
        var (envio, _, _, motivo, regla) = CrearEscenarioEnTransito();

        envio.RegistrarEntregaParcial(
            UsuarioRepartidorId,
            regla,
            new[] { "BLT-A1", "BLT-A2" },
            new[] { new BultoNoEntregadoInput("BLT-A3", motivo) },
            "Lucia Gomez",
            "41234567",
            "BASE64FIRMA",
            null,
            -34.9011,
            -56.1645,
            DateTime.UtcNow,
            UsuarioRepartidorId);

        var listHandler = new ListarEnviosPendientesDeRutaQueryHandler(
            new FakeEnvioParcialRepo(envio, regla, motivo),
            new FakeTenantParcial(OperadorId, UsuarioRepartidorId));

        var pendientes = await listHandler.Handle(new ListarEnviosPendientesDeRutaQuery(), CancellationToken.None);

        Assert.Single(pendientes);
        var item = pendientes[0];
        Assert.Equal(envio.Id, item.EnvioId);
        // Solo el bulto BLT-A3 (4 kg) está pendiente de reenvío, por lo que el peso operativo para la nueva ruta es 4 kg
        Assert.Equal(4m, envio.PesoTotal);
    }

    [Fact]
    public async Task ProcesarMensaje_SuscripcionV1YV2_VersionaPayloadSinRomperSistemasExistentesDelComercio()
    {
        var (envio, _, _, motivo, regla) = CrearEscenarioEnTransito();
        var suscripcionV1 = new SuscripcionAviso(
            Guid.NewGuid(),
            ComercioOperadorId,
            "EntregadoParcialmente",
            "https://comercio.test/webhooks/v1",
            "clave-secreta-hmac-v1",
            SuscripcionAviso.ContratoV1);

        var suscripcionV2 = new SuscripcionAviso(
            Guid.NewGuid(),
            ComercioOperadorId,
            "EventoBulto",
            "https://comercio.test/webhooks/v2",
            "clave-secreta-hmac-v2",
            SuscripcionAviso.ContratoV2);

        var avisoRepo = new FakeAvisoParcialRepo(new[] { suscripcionV1, suscripcionV2 });
        var webhookSender = new FakeWebhookSenderParcial();
        var uow = new FakeUnitOfWorkParcial();
        var entregador = new AvisoComercioEntregador(
            avisoRepo,
            webhookSender,
            uow,
            new FakeClockParcial());

        var handler = new ProcesarMensajeCommandHandler(
            new FakeIdempotenciaParcialRepo(),
            new FakeEnvioParcialRepo(envio, regla, motivo),
            new FakeDestinatarioRepository(),
            new FakeNotificacionParcialRepo(),
            new FakeNotificacionServiceParcial(),
            avisoRepo,
            entregador,
            uow,
            new FakeTenantParcial(OperadorId, UsuarioRepartidorId),
            new FakeCacheParcialService());

        var eventoPublicado = new EventoEnvioPublicado(
            OperadorId,
            envio.Id,
            ComercioOperadorId,
            envio.CodigoSeguimientoPublico,
            "EntregaParcial",
            "EnTransito",
            "EntregadoParcialmente",
            DateTime.UtcNow,
            "CORR-WEBHOOK-V1V2",
            new[]
            {
                new EventoBultoPublicado("BLT-A1", "Entregado", null),
                new EventoBultoPublicado("BLT-A2", "PendienteReenvio", "RECHAZO_PARCIAL")
            });

        var cmd = new ProcesarMensajeCommand(
            Guid.NewGuid(),
            nameof(Envio),
            envio.Id,
            EventoEnvioPublicado.TipoMensaje,
            eventoPublicado.Serializar());

        var resultado = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(EstadoProcesamientoMensaje.Procesado, resultado.Estado);
        Assert.Equal(2, avisoRepo.AvisosGuardados.Count);

        var avisoV1 = avisoRepo.AvisosGuardados.Single(a => a.SuscripcionAvisoId == suscripcionV1.Id);
        var avisoV2 = avisoRepo.AvisosGuardados.Single(a => a.SuscripcionAvisoId == suscripcionV2.Id);

        using var docV1 = JsonDocument.Parse(avisoV1.Payload);
        Assert.False(docV1.RootElement.TryGetProperty("bultos", out _));
        Assert.False(docV1.RootElement.TryGetProperty("versionContrato", out _));
        Assert.Equal("UM-PARCIAL-APP-01", docV1.RootElement.GetProperty("codigoSeguimiento").GetString());

        using var docV2 = JsonDocument.Parse(avisoV2.Payload);
        Assert.Equal("v2", docV2.RootElement.GetProperty("versionContrato").GetString());
        Assert.Equal(2, docV2.RootElement.GetProperty("resumenBultos").GetProperty("totalBultos").GetInt32());
        Assert.Equal(1, docV2.RootElement.GetProperty("resumenBultos").GetProperty("bultosEntregados").GetInt32());
        var bultosJson = docV2.RootElement.GetProperty("bultos");
        Assert.Equal(2, bultosJson.GetArrayLength());
    }

    private sealed class FakeEnvioParcialRepo : IEnvioRepository
    {
        private readonly Envio _envio;
        private readonly ReglaOperativa? _regla;
        private readonly MotivoNoEntrega? _motivo;

        public FakeEnvioParcialRepo(Envio envio, ReglaOperativa? regla, MotivoNoEntrega? motivo)
        {
            _envio = envio;
            _regla = regla;
            _motivo = motivo;
        }

        public Task AddAsync(Envio envio, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExisteCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Equals(_envio.CodigoSeguimientoPublico, codigoSeguimiento, StringComparison.OrdinalIgnoreCase));
        public Task<(List<Envio> Items, int Total)> BuscarPorComercioAsync(Guid operadorId, Guid comercioOperadorId, FiltroEnviosComercio filtro, CancellationToken cancellationToken = default) =>
            Task.FromResult((new List<Envio> { _envio }, 1));
        public Task<List<Envio>> ListarPorEstadoAsync(Guid operadorId, EstadoEnvio estado, CancellationToken cancellationToken = default) =>
            Task.FromResult(_envio.OperadorId == operadorId && _envio.EstadoActual == estado ? new List<Envio> { _envio } : new List<Envio>());
        public Task<Envio?> GetByReferenciaExternaAsync(Guid comercioOperadorId, string referenciaExterna, CancellationToken cancellationToken = default) =>
            Task.FromResult<Envio?>(null);
        public Task<Envio?> GetByCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Equals(_envio.CodigoSeguimientoPublico, codigoSeguimiento, StringComparison.OrdinalIgnoreCase) ? _envio : null);
        public Task<List<Envio>> ListarRecientesPorOperadorAsync(Guid operadorId, int maximo, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Envio> { _envio });
        public Task<Envio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == _envio.Id ? _envio : null);
        public Task<List<Envio>> GetByHojaDeRutaIdAsync(Guid hojaDeRutaId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Envio> { _envio });
        public Task<List<Envio>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default) =>
            Task.FromResult(ids.Contains(_envio.Id) ? new List<Envio> { _envio } : new List<Envio>());
        public Task UpdateAsync(Envio envio, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateRangeAsync(IEnumerable<Envio> envios, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ReglaOperativa?> GetReglaOperativaVigenteAsync(Guid operadorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_regla);
        public Task<List<MotivoNoEntrega>> ListarMotivosNoEntregaActivosAsync(Guid operadorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_motivo is not null ? new List<MotivoNoEntrega> { _motivo } : new List<MotivoNoEntrega>());
        public Task<MotivoNoEntrega?> GetMotivoNoEntregaAsync(Guid operadorId, Guid? motivoId, string? codigo = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(_motivo is not null && (
                (motivoId.HasValue && _motivo.Id == motivoId.Value) ||
                (!string.IsNullOrWhiteSpace(codigo) && string.Equals(_motivo.Codigo, codigo.Trim(), StringComparison.OrdinalIgnoreCase)))
                ? _motivo
                : null);
    }

    private sealed class FakeRutaParcialRepo : IRutaRepository
    {
        private readonly HojaDeRuta _ruta;
        public FakeRutaParcialRepo(HojaDeRuta ruta) => _ruta = ruta;

        public Task<HojaDeRuta?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == _ruta.Id ? _ruta : null);
        public Task<List<HojaDeRuta>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default) =>
            Task.FromResult(ids.Contains(_ruta.Id) ? new List<HojaDeRuta> { _ruta } : new List<HojaDeRuta>());
        public Task<HojaDeRuta?> GetByIdWithParadasYAsignacionesAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == _ruta.Id ? _ruta : null);
        public Task<bool> ExisteRutaSinRendirAsync(Guid repartidorId, Guid excluirHojaDeRutaId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
        public Task<List<HojaDeRuta>> GetByRepartidorYFechaAsync(Guid repartidorId, DateOnly fecha, IReadOnlyCollection<string> estados, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<HojaDeRuta> { _ruta });
        public Task<HojaDeRuta?> GetRutaSinRendirAsync(Guid repartidorId, Guid excluirHojaDeRutaId, CancellationToken cancellationToken = default) =>
            Task.FromResult<HojaDeRuta?>(null);
        public Task<List<HojaDeRuta>> ListarAsync(Guid operadorId, DateOnly? fecha, Guid? repartidorId, IReadOnlyCollection<string>? estados, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<HojaDeRuta> { _ruta });
        public Task AddAsync(HojaDeRuta hojaDeRuta, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(HojaDeRuta hojaDeRuta, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<HojaDeRuta>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, Guid operadorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ids.Contains(_ruta.Id) ? new List<HojaDeRuta> { _ruta } : new List<HojaDeRuta>());
    }

    private sealed class FakeOutboxParcialStorage : IOutboxStorage
    {
        public List<OutboxMessage> Mensajes { get; } = new();
        public Task SaveAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            Mensajes.Add(message);
            return Task.CompletedTask;
        }
        public Task SaveRangeAsync(IEnumerable<OutboxMessage> messages, CancellationToken cancellationToken = default)
        {
            Mensajes.AddRange(messages);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCacheParcialService : IDistributedCacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTenantParcial : ICurrentTenant
    {
        public FakeTenantParcial(Guid operadorId, Guid usuarioId)
        {
            OperadorId = operadorId;
            UsuarioId = usuarioId;
        }
        public Guid OperadorId { get; private set; }
        public Guid? UsuarioId { get; }
        public Guid? RepartidorId => null;
        public Guid? ComercioId => null;
        public Guid? ComercioOperadorId => null;
        public string? Rol => "Repartidor";
        public IReadOnlySet<string> Permisos { get; } = new HashSet<string>();
        public bool IsAuthenticated => true;
        public bool TienePermiso(string codigoPermiso) => true;
        public void EstablecerOperadorDeProceso(Guid operadorId) => OperadorId = operadorId;
    }

    private sealed class FakeUnitOfWorkParcial : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeClockParcial : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed class FakeIdempotenciaParcialRepo : IIdempotenciaConsumidorRepository
    {
        private readonly HashSet<string> _claves = new();
        public Task<bool> ExisteAsync(string mensajeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_claves.Contains(mensajeId));
        public Task AddAsync(ClaveIdempotenciaConsumidor clave, CancellationToken cancellationToken = default)
        {
            _claves.Add(clave.MensajeId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotificacionParcialRepo : INotificacionRepository
    {
        public Task AddAsync(Notificacion notificacion, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeNotificacionServiceParcial : INotificacionDestinatarioService
    {
        public Task EnviarAsync(NotificacionDestinatario notificacion, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAvisoParcialRepo : IAvisoComercioRepository
    {
        private readonly List<SuscripcionAviso> _suscripciones;
        public List<AvisoComercio> AvisosGuardados { get; } = new();

        public FakeAvisoParcialRepo(IEnumerable<SuscripcionAviso> suscripciones) => _suscripciones = suscripciones.ToList();

        public Task<List<SuscripcionAviso>> ListarSuscripcionesActivasAsync(Guid comercioOperadorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_suscripciones.Where(s => s.ComercioOperadorId == comercioOperadorId && s.Activa).ToList());
        public Task<SuscripcionAviso?> GetSuscripcionAsync(Guid suscripcionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_suscripciones.FirstOrDefault(s => s.Id == suscripcionId));
        public Task<List<SuscripcionAviso>> ListarSuscripcionesAsync(Guid? comercioOperadorId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(_suscripciones);
        public Task<SuscripcionAviso?> GetSuscripcionParaEdicionAsync(Guid suscripcionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_suscripciones.FirstOrDefault(s => s.Id == suscripcionId));
        public Task AddSuscripcionAsync(SuscripcionAviso suscripcion, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<AvisoComercio>> ListarHistorialAvisosAsync(Guid? comercioOperadorId = null, string? estado = null, int limite = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult(AvisosGuardados);
        public Task<AvisoComercio?> GetAvisoByIdAsync(Guid avisoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(AvisosGuardados.FirstOrDefault(a => a.Id == avisoId));
        public Task AddAsync(AvisoComercio aviso, CancellationToken cancellationToken = default)
        {
            AvisosGuardados.Add(aviso);
            return Task.CompletedTask;
        }
        public Task<List<AvisoComercio>> ListarPendientesDeEntregaAsync(DateTime ahoraUtc, int maximo, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AvisoComercio>());
    }

    private sealed class FakeWebhookSenderParcial : IWebhookSender
    {
        public Task<ResultadoWebhook> EnviarAsync(string urlWebhook, string payload, string firma, Guid avisoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResultadoWebhook(true, 200, "HTTP 200 OK"));
    }
}
