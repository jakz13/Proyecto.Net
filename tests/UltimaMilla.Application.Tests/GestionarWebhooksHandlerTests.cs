using UltimaMilla.Application.Commands.Mensajeria;
using UltimaMilla.Application.Commands.Mensajeria.GestionarWebhooks;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

/// <summary>
/// Pruebas unitarias de los casos de uso de gestión de suscripciones de webhooks (UC-36),
/// consulta de historial y reenvío manual de avisos agotados o fallidos (UC-30).
/// </summary>
public class GestionarWebhooksHandlerTests
{
    private readonly FakeAvisoRepo _avisosRepo = new();
    private readonly FakeComercioOperadorRepository _comerciosRepo = new();
    private readonly FakeWebhookSenderLocal _webhookSender = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();
    private readonly ComercioOperador _comercioDelOperador;
    private readonly ComercioOperador _comercioDeOtroOperador;

    public GestionarWebhooksHandlerTests()
    {
        _comercioDelOperador = new ComercioOperador(Guid.NewGuid(), Guid.NewGuid(), _tenant.OperadorId, "huella-op1");
        _comercioDeOtroOperador = new ComercioOperador(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "huella-op2");
        _comerciosRepo.Relaciones.Add(_comercioDelOperador);
        _comerciosRepo.Relaciones.Add(_comercioDeOtroOperador);
    }

    private CrearSuscripcionAvisoCommandHandler CrearAltaHandler() =>
        new(_avisosRepo, _comerciosRepo, _tenant, _uow);

    private DesactivarSuscripcionAvisoCommandHandler CrearBajaHandler() =>
        new(_avisosRepo, _uow);

    private ReenviarAvisoManualCommandHandler CrearReenvioHandler() =>
        new(_avisosRepo, new AvisoComercioEntregador(_avisosRepo, _webhookSender, _uow, _clock));

    [Fact]
    public async Task CrearSuscripcion_DatosValidos_CreaSuscripcionActivaYPersiste()
    {
        var cmd = new CrearSuscripcionAvisoCommand(
            _comercioDelOperador.Id,
            "EnvioEntregado",
            "https://comercio.ejemplo.com/webhooks/entregas",
            "clave-secreta-hmac-2026");

        var dto = await CrearAltaHandler().Handle(cmd, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal(_comercioDelOperador.Id, dto.ComercioOperadorId);
        Assert.Equal("EnvioEntregado", dto.TipoEvento);
        Assert.Equal("https://comercio.ejemplo.com/webhooks/entregas", dto.UrlWebhook);
        Assert.True(dto.Activa);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Theory]
    [InlineData("ftp://comercio.ejemplo.com/hook")]
    [InlineData("no-es-una-url")]
    [InlineData("")]
    public async Task CrearSuscripcion_UrlInvalida_LanzaDomainValidationException(string urlInvalida)
    {
        var cmd = new CrearSuscripcionAvisoCommand(
            _comercioDelOperador.Id,
            "*",
            urlInvalida,
            "clave-secreta-hmac-2026");

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearAltaHandler().Handle(cmd, CancellationToken.None));
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task CrearSuscripcion_ClaveCorta_LanzaDomainValidationException()
    {
        var cmd = new CrearSuscripcionAvisoCommand(
            _comercioDelOperador.Id,
            "*",
            "https://comercio.ejemplo.com/hook",
            "corta");

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearAltaHandler().Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task CrearSuscripcion_ComercioDeOtroOperador_LanzaUnauthorizedAccessException()
    {
        var cmd = new CrearSuscripcionAvisoCommand(
            _comercioDeOtroOperador.Id,
            "*",
            "https://comercio.ejemplo.com/hook",
            "clave-secreta-hmac-2026");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearAltaHandler().Handle(cmd, CancellationToken.None));
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task DesactivarSuscripcion_Existente_LaMarcaInactiva()
    {
        var suscripcion = new SuscripcionAviso(
            Guid.NewGuid(),
            _comercioDelOperador.Id,
            "*",
            "https://comercio.ejemplo.com/hook",
            "clave-secreta-hmac-2026");
        _avisosRepo.Suscripciones.Add(suscripcion);

        var dto = await CrearBajaHandler().Handle(
            new DesactivarSuscripcionAvisoCommand(suscripcion.Id, _comercioDelOperador.Id),
            CancellationToken.None);

        Assert.False(dto.Activa);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task ReenviarAvisoManual_AvisoAgotado_ReiniciaYEntregaExitosamente()
    {
        var suscripcion = new SuscripcionAviso(
            Guid.NewGuid(),
            _comercioDelOperador.Id,
            "*",
            "https://comercio.ejemplo.com/hook",
            "clave-secreta-hmac-2026");
        _avisosRepo.Suscripciones.Add(suscripcion);

        var aviso = new AvisoComercio(
            Guid.NewGuid(),
            suscripcion.Id,
            Guid.NewGuid(),
            "{\"evento\":\"EnvioEntregado\"}",
            "sha256=abc123");
        aviso.MarcarAgotado("HTTP 500 Internal Server Error");
        _avisosRepo.Avisos.Add(aviso);

        var resultado = await CrearReenvioHandler().Handle(
            new ReenviarAvisoManualCommand(aviso.Id),
            CancellationToken.None);

        Assert.Equal("Entregado", resultado.Estado);
        Assert.Equal(1, resultado.Intentos);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task ReenviarAvisoManual_AvisoYaEntregado_LanzaDomainValidationException()
    {
        var suscripcion = new SuscripcionAviso(
            Guid.NewGuid(),
            _comercioDelOperador.Id,
            "*",
            "https://comercio.ejemplo.com/hook",
            "clave-secreta-hmac-2026");
        _avisosRepo.Suscripciones.Add(suscripcion);

        var aviso = new AvisoComercio(
            Guid.NewGuid(),
            suscripcion.Id,
            Guid.NewGuid(),
            "{\"evento\":\"EnvioEntregado\"}",
            "sha256=abc123");
        aviso.MarcarEntregado("HTTP 200 OK");
        _avisosRepo.Avisos.Add(aviso);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearReenvioHandler().Handle(new ReenviarAvisoManualCommand(aviso.Id), CancellationToken.None));
    }

    private sealed class FakeAvisoRepo : IAvisoComercioRepository
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
            Task.FromResult(Avisos.Take(maximo).ToList());

        public Task<List<SuscripcionAviso>> ListarSuscripcionesAsync(Guid? comercioOperadorId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Suscripciones
                .Where(s => !comercioOperadorId.HasValue || comercioOperadorId.Value == Guid.Empty || s.ComercioOperadorId == comercioOperadorId.Value)
                .ToList());

        public Task<SuscripcionAviso?> GetSuscripcionParaEdicionAsync(Guid suscripcionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Suscripciones.FirstOrDefault(s => s.Id == suscripcionId));

        public Task AddSuscripcionAsync(SuscripcionAviso suscripcion, CancellationToken cancellationToken = default)
        {
            Suscripciones.Add(suscripcion);
            return Task.CompletedTask;
        }

        public Task<List<AvisoComercio>> ListarHistorialAvisosAsync(Guid? comercioOperadorId = null, string? estado = null, int limite = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult(Avisos
                .Where(a => string.IsNullOrWhiteSpace(estado) || a.Estado == estado)
                .Take(limite)
                .ToList());

        public Task<AvisoComercio?> GetAvisoByIdAsync(Guid avisoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Avisos.FirstOrDefault(a => a.Id == avisoId));
    }

    private sealed class FakeWebhookSenderLocal : IWebhookSender
    {
        public Task<ResultadoWebhook> EnviarAsync(string urlWebhook, string payload, string firma, Guid avisoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResultadoWebhook(true, 200, "HTTP 200 OK"));
    }
}

