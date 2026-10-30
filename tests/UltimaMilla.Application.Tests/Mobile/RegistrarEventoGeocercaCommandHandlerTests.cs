using UltimaMilla.Application.Commands.Mobile.Navegacion.RegistrarEventoGeocerca;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Seguimiento.ConsultarSeguimientoPublico;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests.Mobile;

public class RegistrarEventoGeocercaCommandHandlerTests
{
    private static readonly Guid OperadorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtroOperadorId = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid ComercioOperadorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UsuarioRepartidorId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid HojaDeRutaId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private static readonly Guid Parada1Id = Guid.Parse("bbbbbbbb-1111-2222-3333-000000000001");
    private static readonly Guid Envio1Id = Guid.Parse("cccccccc-1111-2222-3333-000000000001");
    private static readonly Guid Direccion1Id = Guid.Parse("eeeeeeee-0001-0000-0000-000000000001");

    private static (
        HojaDeRuta Ruta,
        Envio Envio,
        FakeRutaRepository RutaRepo,
        FakeEnvioRepository EnvioRepo,
        FakeOutboxStorage Outbox,
        FakeUnitOfWork Uow,
        FakeDistributedCache Cache,
        RegistrarEventoGeocercaCommandHandler Handler) CrearEscenario(Guid? tenantOperadorId = null)
    {
        var ruta = new HojaDeRuta(HojaDeRutaId, OperadorId, DateOnly.FromDateTime(DateTime.UtcNow), "Despachada");
        ruta.AgregarParada(Parada1Id, Envio1Id, 1, Direccion1Id, null, "Pendiente");

        var envio = new Envio(
            Envio1Id,
            ComercioOperadorId,
            "Av. 18 de Julio 1450, Centro, Montevideo",
            "Martín Rodríguez",
            "ENV-2026-00108",
            OperadorId);
        envio.AgregarBulto("BLT-001-A", 1.2m);
        envio.RecepcionarEnDeposito(new[] { "BLT-001-A" }, null, null, UsuarioRepartidorId);
        envio.ClasificarBultos(new[] { new ClasificacionBulto("BLT-001-A", PrioridadBulto.Normal) }, UsuarioRepartidorId);
        envio.AsignarRuta(HojaDeRutaId);
        envio.DespacharEnRuta(HojaDeRutaId, UsuarioRepartidorId, "Despacho inicial");

        var rutaRepo = new FakeRutaRepository(ruta);
        var envioRepo = new FakeEnvioRepository(envio);
        var outbox = new FakeOutboxStorage();
        var uow = new FakeUnitOfWork();
        var cache = new FakeDistributedCache();
        var tenant = new FakeCurrentTenant(tenantOperadorId ?? OperadorId, UsuarioRepartidorId);
        var clock = new FakeClock();

        var handler = new RegistrarEventoGeocercaCommandHandler(
            rutaRepo,
            envioRepo,
            outbox,
            uow,
            cache,
            tenant,
            clock);

        return (ruta, envio, rutaRepo, envioRepo, outbox, uow, cache, handler);
    }

    [Fact]
    public async Task Handle_LlegadaYSalidaGeocerca_RegistraEventosIdempotentesYReflejaEnSeguimientoPublico()
    {
        var (_, envio, _, envioRepo, outbox, uow, cache, handler) = CrearEscenario();

        // 1. Entrada a Geocerca (< 100m -> 45m)
        var cmdLlegada = new RegistrarEventoGeocercaCommand(
            HojaDeRutaId,
            Parada1Id,
            Envio1Id,
            "LLEGADA_GEOCERCA",
            -34.905395,
            -56.1885,
            45.0);

        var resLlegada = await handler.Handle(cmdLlegada, CancellationToken.None);

        Assert.False(resLlegada.Idempotente);
        Assert.Equal("LlegadaGeocerca", resLlegada.TipoEvento);
        Assert.Contains("Geocerca < 100m", resLlegada.DescripcionPublica);
        Assert.Equal(1, uow.SaveCount);
        Assert.Single(outbox.Messages);
        Assert.Contains("seguimiento:ENV-2026-00108", cache.RemovedKeys);

        // 2. Segunda lectura consecutiva en < 100m no duplica el evento (Histéresis / Idempotencia)
        var resLlegadaDuplicada = await handler.Handle(cmdLlegada, CancellationToken.None);
        Assert.True(resLlegadaDuplicada.Idempotente);
        Assert.Equal(1, uow.SaveCount);

        // 3. Salida de Geocerca (> 150m -> 220m, permanencia 120s)
        var cmdSalida = new RegistrarEventoGeocercaCommand(
            HojaDeRutaId,
            Parada1Id,
            Envio1Id,
            "SALIDA_GEOCERCA",
            -34.903820,
            -56.1885,
            220.0,
            TiempoPermanenciaSegundos: 120);

        var resSalida = await handler.Handle(cmdSalida, CancellationToken.None);

        Assert.False(resSalida.Idempotente);
        Assert.Equal("SalidaGeocerca", resSalida.TipoEvento);
        Assert.Contains("Permanencia: 120s", resSalida.DescripcionPublica);
        Assert.Equal(2, uow.SaveCount);

        // 4. Verificar que ConsultarSeguimientoPublicoQueryHandler expone ambos hitos
        var seguimientoHandler = new ConsultarSeguimientoPublicoQueryHandler(envioRepo, new FakeFranjaHorariaRepository());
        var seguimientoDto = await seguimientoHandler.Handle(new ConsultarSeguimientoPublicoQuery("ENV-2026-00108"), CancellationToken.None);

        Assert.NotNull(seguimientoDto);
        Assert.Contains(seguimientoDto!.HistorialEventos, h => h.DescripcionPublica.Contains("Repartidor ingresó a la zona de entrega"));
        Assert.Contains(seguimientoDto.HistorialEventos, h => h.DescripcionPublica.Contains("Repartidor salió de la zona de entrega (Permanencia: 120s)"));

        // 5. Verificar que ReiniciarParaDemoEnTransito limpia los eventos de geocerca
        envio.ReiniciarParaDemoEnTransito(HojaDeRutaId);
        var seguimientoTrasReset = await seguimientoHandler.Handle(new ConsultarSeguimientoPublicoQuery("ENV-2026-00108"), CancellationToken.None);
        Assert.NotNull(seguimientoTrasReset);
        Assert.DoesNotContain(seguimientoTrasReset!.HistorialEventos, h => h.DescripcionPublica.Contains("zona de entrega"));
    }

    [Fact]
    public async Task Handle_ParametrosInvalidosOFueraDeRango_LanzaExcepcionTipada()
    {
        var (_, _, _, _, _, _, _, handler) = CrearEscenario();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, CancellationToken.None));

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(new RegistrarEventoGeocercaCommand(Guid.Empty, Parada1Id, Envio1Id, "LLEGADA_GEOCERCA", -34.9, -56.1, 45), CancellationToken.None));

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(new RegistrarEventoGeocercaCommand(HojaDeRutaId, Guid.Empty, Envio1Id, "LLEGADA_GEOCERCA", -34.9, -56.1, 45), CancellationToken.None));

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(new RegistrarEventoGeocercaCommand(HojaDeRutaId, Parada1Id, Envio1Id, "", -34.9, -56.1, 45), CancellationToken.None));

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(new RegistrarEventoGeocercaCommand(HojaDeRutaId, Parada1Id, Envio1Id, "EVENTO_DESCONOCIDO", -34.9, -56.1, 45), CancellationToken.None));

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(new RegistrarEventoGeocercaCommand(HojaDeRutaId, Parada1Id, Envio1Id, "LLEGADA_GEOCERCA", -95.0, -56.1, 45), CancellationToken.None));

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(new RegistrarEventoGeocercaCommand(HojaDeRutaId, Parada1Id, Envio1Id, "LLEGADA_GEOCERCA", -34.9, -56.1, -10), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_IdentificadoresInexistentesOOtroTenant_LanzaNotFoundOUnauthorized()
    {
        var (_, _, _, _, _, _, _, handler) = CrearEscenario();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new RegistrarEventoGeocercaCommand(Guid.NewGuid(), Parada1Id, Envio1Id, "LLEGADA_GEOCERCA", -34.9, -56.1, 45), CancellationToken.None));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new RegistrarEventoGeocercaCommand(HojaDeRutaId, Guid.NewGuid(), Envio1Id, "LLEGADA_GEOCERCA", -34.9, -56.1, 45), CancellationToken.None));

        var (_, _, _, _, _, _, _, handlerOtroTenant) = CrearEscenario(OtroOperadorId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handlerOtroTenant.Handle(new RegistrarEventoGeocercaCommand(HojaDeRutaId, Parada1Id, Envio1Id, "LLEGADA_GEOCERCA", -34.9, -56.1, 45), CancellationToken.None));
    }

    private sealed class FakeRutaRepository : IRutaRepository
    {
        private readonly HojaDeRuta _ruta;
        public FakeRutaRepository(HojaDeRuta ruta) => _ruta = ruta;
        public Task<HojaDeRuta?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(id == _ruta.Id ? _ruta : null);
        public Task<List<HojaDeRuta>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default) => Task.FromResult(new List<HojaDeRuta>());
        public Task<HojaDeRuta?> GetByIdWithParadasYAsignacionesAsync(Guid id, CancellationToken ct = default) => Task.FromResult(id == _ruta.Id ? _ruta : null);
        public Task<bool> ExisteRutaSinRendirAsync(Guid repartidorId, Guid excluirHojaDeRutaId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<List<HojaDeRuta>> GetByRepartidorYFechaAsync(Guid repartidorId, DateOnly fecha, IReadOnlyCollection<string> estados, CancellationToken ct = default) => Task.FromResult(new List<HojaDeRuta>());
        public Task<HojaDeRuta?> GetRutaSinRendirAsync(Guid repartidorId, Guid excluirHojaDeRutaId, CancellationToken ct = default) => Task.FromResult<HojaDeRuta?>(null);
        public Task<List<HojaDeRuta>> ListarAsync(Guid operadorId, DateOnly? fecha, Guid? repartidorId, IReadOnlyCollection<string>? estados, CancellationToken ct = default) => Task.FromResult(new List<HojaDeRuta>());
        public Task AddAsync(HojaDeRuta hojaDeRuta, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(HojaDeRuta hojaDeRuta, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeEnvioRepository : IEnvioRepository
    {
        private readonly Envio _envio;
        public FakeEnvioRepository(Envio envio) => _envio = envio;
        public Task AddAsync(Envio envio, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> ExisteCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken ct = default) => Task.FromResult(codigoSeguimiento == _envio.CodigoSeguimientoPublico);
        public Task<(List<Envio> Items, int Total)> BuscarPorComercioAsync(Guid operadorId, Guid comercioOperadorId, FiltroEnviosComercio filtro, CancellationToken ct = default) => Task.FromResult((new List<Envio>(), 0));
        public Task<List<Envio>> ListarPorEstadoAsync(Guid operadorId, EstadoEnvio estado, CancellationToken ct = default) => Task.FromResult(new List<Envio>());
        public Task<Envio?> GetByReferenciaExternaAsync(Guid comercioOperadorId, string referenciaExterna, CancellationToken ct = default) => Task.FromResult<Envio?>(null);
        public Task<Envio?> GetByCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken ct = default) => Task.FromResult(codigoSeguimiento == _envio.CodigoSeguimientoPublico ? _envio : null);
        public Task<List<Envio>> ListarRecientesPorOperadorAsync(Guid operadorId, int maximo, CancellationToken ct = default) => Task.FromResult(new List<Envio>());
        public Task<Envio?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(id == _envio.Id ? _envio : null);
        public Task<List<Envio>> GetByHojaDeRutaIdAsync(Guid hojaDeRutaId, CancellationToken ct = default) => Task.FromResult(new List<Envio> { _envio });
        public Task<List<Envio>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default) => Task.FromResult(new List<Envio> { _envio });
        public Task UpdateAsync(Envio envio, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateRangeAsync(IEnumerable<Envio> envios, CancellationToken ct = default) => Task.CompletedTask;
        public Task<ReglaOperativa?> GetReglaOperativaVigenteAsync(Guid operadorId, CancellationToken ct = default) => Task.FromResult<ReglaOperativa?>(null);
        public Task<List<MotivoNoEntrega>> ListarMotivosNoEntregaActivosAsync(Guid operadorId, CancellationToken ct = default) => Task.FromResult(new List<MotivoNoEntrega>());
        public Task<MotivoNoEntrega?> GetMotivoNoEntregaAsync(Guid operadorId, Guid? motivoId, string? codigo = null, CancellationToken ct = default) => Task.FromResult<MotivoNoEntrega?>(null);
    }

    private sealed class FakeFranjaHorariaRepository : IFranjaHorariaRepository
    {
        public Task AddAsync(FranjaHoraria franja, CancellationToken ct = default) => Task.CompletedTask;
        public Task<FranjaHoraria?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<FranjaHoraria?>(null);
        public Task<List<FranjaHoraria>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default) => Task.FromResult(new List<FranjaHoraria>());
        public Task<List<FranjaHoraria>> ListarPorOperadorAsync(Guid operadorId, bool soloActivas = false, CancellationToken ct = default) => Task.FromResult(new List<FranjaHoraria>());
        public Task<List<FranjaHoraria>> ListarActivasPorZonaAsync(Guid zonaCoberturaId, CancellationToken ct = default) => Task.FromResult(new List<FranjaHoraria>());
        public Task UpdateAsync(FranjaHoraria franja, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeOutboxStorage : IOutboxStorage
    {
        public List<OutboxMessage> Messages { get; } = new();
        public Task SaveAsync(OutboxMessage message, CancellationToken ct = default) { Messages.Add(message); return Task.CompletedTask; }
        public Task SaveRangeAsync(IEnumerable<OutboxMessage> messages, CancellationToken ct = default) { Messages.AddRange(messages); return Task.CompletedTask; }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct = default) { SaveCount++; return Task.FromResult(1); }
    }

    private sealed class FakeDistributedCache : IDistributedCacheService
    {
        public List<string> RemovedKeys { get; } = new();
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => Task.FromResult<T?>(default);
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken ct = default) { RemovedKeys.Add(key); return Task.CompletedTask; }
    }

    private sealed class FakeCurrentTenant : ICurrentTenant
    {
        public FakeCurrentTenant(Guid operadorId, Guid usuarioId) { OperadorId = operadorId; UsuarioId = usuarioId; }
        public Guid OperadorId { get; }
        public Guid? UsuarioId { get; }
        public Guid? ComercioOperadorId => null;
        public bool IsAuthenticated => true;
    }

    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
