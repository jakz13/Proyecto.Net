using UltimaMilla.Application.Commands.Comercio.ReintentarAviso;
using UltimaMilla.Application.Commands.Mensajeria;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Comercio.ConsultarHistorialAvisos;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

/// <summary>
/// Pruebas exhaustivas para el caso de uso UC-30 Consultar historial de entregas de avisos y reintentar.
/// </summary>
public class HistorialReintentoAvisosHandlerTests
{
    private readonly FakeSuscripcionAvisoRepository _suscripcionesRepo = new();
    private readonly FakeAvisoComercioConsultaRepository _avisosRepo;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly SenderConfigurable _sender = new();

    public HistorialReintentoAvisosHandlerTests()
    {
        _avisosRepo = new FakeAvisoComercioConsultaRepository(_suscripcionesRepo);
    }

    private ReintentarAvisoCommandHandler CrearReintentarHandler() =>
        new(_avisosRepo, _suscripcionesRepo, _tenant,
            new AvisoComercioEntregador(new RepoWorkerSobreSuscripciones(_suscripcionesRepo), _sender, _unitOfWork, new FakeClock()));

    private ConsultarHistorialAvisosQueryHandler CrearConsultarHandler() =>
        new(_avisosRepo, _suscripcionesRepo, _tenant);

    [Fact]
    public async Task ReintentarAviso_AvisoFallido_ReenviaExitosamenteIncrementaIntentosYConfirmaUow()
    {
        // Arrange
        var comercioId = _tenant.ComercioOperadorId!.Value;
        var suscripcion = new SuscripcionAviso(Guid.NewGuid(), comercioId, "NoEntregado", "https://comercio.uy/webhook", "clave123");
        await _suscripcionesRepo.AddAsync(suscripcion);

        var aviso = new AvisoComercio(Guid.NewGuid(), suscripcion.Id, Guid.NewGuid(), "{\"evento\":\"NoEntregado\"}", "firma123", "Fallido");
        aviso.RegistrarFallo("HTTP 500 Server Error", DateTime.UtcNow.AddMinutes(5));
        await _avisosRepo.AddAsync(aviso);

        var handler = CrearReintentarHandler();

        // Act
        var resultado = await handler.Handle(new ReintentarAvisoCommand(aviso.Id), CancellationToken.None);

        // Assert
        Assert.NotNull(resultado);
        Assert.True(resultado.Exitoso);
        Assert.Equal("Entregado", resultado.Estado);
        Assert.Equal(200, resultado.CodigoHttp);
        Assert.Equal(1, _sender.Envios);
        Assert.True(resultado.Intentos >= 2);
        Assert.True(_unitOfWork.Confirmaciones >= 1);

        var avisoActualizado = await _avisosRepo.ObtenerPorIdAsync(aviso.Id);
        Assert.NotNull(avisoActualizado);
        Assert.Equal("Entregado", avisoActualizado.Estado);
    }

    [Fact]
    public async Task ReintentarAviso_ReceptorSigueCaido_NoLoDaPorEntregadoYRegistraLaFalla()
    {
        var comercioId = _tenant.ComercioOperadorId!.Value;
        var suscripcion = new SuscripcionAviso(Guid.NewGuid(), comercioId, "Entregado", "https://comercio.uy/webhook", "clave123");
        await _suscripcionesRepo.AddAsync(suscripcion);

        var aviso = new AvisoComercio(Guid.NewGuid(), suscripcion.Id, Guid.NewGuid(), "{\"test\":true}", "firma", "Fallido");
        aviso.MarcarAgotado("HTTP 503");
        await _avisosRepo.AddAsync(aviso);
        _sender.Resultado = new ResultadoWebhook(false, 503, "HTTP 503 Service Unavailable");

        var resultado = await CrearReintentarHandler().Handle(new ReintentarAvisoCommand(aviso.Id), CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.NotEqual("Entregado", resultado.Estado);
        Assert.Equal(503, resultado.CodigoHttp);
        Assert.Equal(1, _sender.Envios);
    }

    private sealed class SenderConfigurable : IWebhookSender
    {
        public ResultadoWebhook Resultado { get; set; } = new(true, 200, "HTTP 200 OK");
        public int Envios { get; private set; }

        public Task<ResultadoWebhook> EnviarAsync(string urlWebhook, string payload, string firma, Guid avisoId, CancellationToken cancellationToken = default)
        {
            Envios++;
            return Task.FromResult(Resultado);
        }
    }

    /// <summary>Repositorio del worker que solo resuelve suscripciones, apoyado en el fake del repositorio de consulta.</summary>
    private sealed class RepoWorkerSobreSuscripciones : IAvisoComercioRepository
    {
        private readonly FakeSuscripcionAvisoRepository _suscripciones;
        public RepoWorkerSobreSuscripciones(FakeSuscripcionAvisoRepository suscripciones) => _suscripciones = suscripciones;

        public Task<SuscripcionAviso?> GetSuscripcionAsync(Guid suscripcionId, CancellationToken cancellationToken = default) =>
            _suscripciones.ObtenerPorIdAsync(suscripcionId, cancellationToken);

        public Task<List<SuscripcionAviso>> ListarSuscripcionesActivasAsync(Guid comercioOperadorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(AvisoComercio aviso, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<AvisoComercio>> ListarPendientesDeEntregaAsync(DateTime ahoraUtc, int maximo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<SuscripcionAviso>> ListarSuscripcionesAsync(Guid? comercioOperadorId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SuscripcionAviso?> GetSuscripcionParaEdicionAsync(Guid suscripcionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddSuscripcionAsync(SuscripcionAviso suscripcion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<AvisoComercio>> ListarHistorialAvisosAsync(Guid? comercioOperadorId = null, string? estado = null, int limite = 50, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AvisoComercio?> GetAvisoByIdAsync(Guid avisoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task ReintentarAviso_IdVacio_LanzaDomainValidationException()
    {
        var handler = CrearReintentarHandler();
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(new ReintentarAvisoCommand(Guid.Empty), CancellationToken.None));
    }

    [Fact]
    public async Task ReintentarAviso_IdInexistente_LanzaKeyNotFoundException()
    {
        var handler = CrearReintentarHandler();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new ReintentarAvisoCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task ReintentarAviso_AvisoYaEntregado_LanzaDomainValidationException()
    {
        // Arrange
        var comercioId = _tenant.ComercioOperadorId!.Value;
        var suscripcion = new SuscripcionAviso(Guid.NewGuid(), comercioId, "Entregado", "https://comercio.uy/webhook", "clave123");
        await _suscripcionesRepo.AddAsync(suscripcion);

        var aviso = new AvisoComercio(Guid.NewGuid(), suscripcion.Id, Guid.NewGuid(), "{\"test\":true}", "firma", "Entregado");
        aviso.MarcarEntregado("HTTP 200 OK");
        await _avisosRepo.AddAsync(aviso);

        var handler = CrearReintentarHandler();

        // Act & Assert
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(new ReintentarAvisoCommand(aviso.Id), CancellationToken.None));
    }

    [Fact]
    public async Task ReintentarAviso_AntiIdor_AvisoDeOtroComercio_LanzaUnauthorizedAccessException()
    {
        // Arrange: la suscripción pertenece a OTRO comercio
        var otroComercioId = Guid.NewGuid();
        var suscripcion = new SuscripcionAviso(Guid.NewGuid(), otroComercioId, "Entregado", "https://otro.uy/webhook", "clave123");
        await _suscripcionesRepo.AddAsync(suscripcion);

        var aviso = new AvisoComercio(Guid.NewGuid(), suscripcion.Id, Guid.NewGuid(), "{\"test\":true}", "firma", "Fallido");
        await _avisosRepo.AddAsync(aviso);

        var handler = CrearReintentarHandler();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new ReintentarAvisoCommand(aviso.Id), CancellationToken.None));
    }

    [Fact]
    public async Task ReintentarAviso_UsuarioSinComercio_LanzaUnauthorizedAccessException()
    {
        _tenant.ComercioOperadorId = null;
        var handler = CrearReintentarHandler();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new ReintentarAvisoCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task ConsultarHistorial_FiltraPorEstadoYCodigoSeguimiento()
    {
        // Arrange
        var comercioId = _tenant.ComercioOperadorId!.Value;
        var suscripcion = new SuscripcionAviso(Guid.NewGuid(), comercioId, "Entregado", "https://comercio.uy/webhook", "clave123");
        await _suscripcionesRepo.AddAsync(suscripcion);

        var envioId1 = Guid.NewGuid();
        var aviso1 = new AvisoComercio(Guid.NewGuid(), suscripcion.Id, envioId1, "{\"codigoSeguimiento\":\"TRK-2026-9999\"}", "firma", "Entregado");
        var aviso2 = new AvisoComercio(Guid.NewGuid(), suscripcion.Id, Guid.NewGuid(), "{\"codigoSeguimiento\":\"TRK-2026-8888\"}", "firma", "Fallido");

        await _avisosRepo.AddAsync(aviso1);
        await _avisosRepo.AddAsync(aviso2);

        var handler = CrearConsultarHandler();

        // Act
        var todos = await handler.Handle(new ConsultarHistorialAvisosQuery(), CancellationToken.None);
        var soloFallidos = await handler.Handle(new ConsultarHistorialAvisosQuery(Estado: "Fallido"), CancellationToken.None);
        var porTracking = await handler.Handle(new ConsultarHistorialAvisosQuery(CodigoSeguimiento: "9999"), CancellationToken.None);

        // Assert
        Assert.Equal(2, todos.Count);
        Assert.Single(soloFallidos);
        Assert.Equal("Fallido", soloFallidos[0].Estado);
        Assert.Single(porTracking);
        Assert.Equal("TRK-2026-9999", porTracking[0].CodigoSeguimiento);
    }

    [Fact]
    public async Task ConsultarHistorial_UsuarioSinComercio_LanzaUnauthorizedAccessException()
    {
        _tenant.ComercioOperadorId = null;
        var handler = CrearConsultarHandler();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new ConsultarHistorialAvisosQuery(), CancellationToken.None));
    }
}

