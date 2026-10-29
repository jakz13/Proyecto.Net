using UltimaMilla.Application.Commands.Mobile.Posicion.RegistrarPosiciones;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class RegistrarPosicionesCommandHandlerTests
{
    private sealed class FakePosicionRepository : IPosicionVehiculoRepository
    {
        public List<PosicionVehiculo> Posiciones { get; } = new();

        public Task AddRangeAsync(IEnumerable<PosicionVehiculo> posiciones, CancellationToken cancellationToken = default)
        {
            Posiciones.AddRange(posiciones);
            return Task.CompletedTask;
        }
    }

    private readonly FakeRepartidorRepository _repartidores = new();
    private readonly FakeRutaRepository _rutas = new();
    private readonly FakePosicionRepository _posiciones = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();
    private readonly Guid _vehiculoId = Guid.NewGuid();
    private readonly Repartidor _repartidor;

    public RegistrarPosicionesCommandHandlerTests()
    {
        _repartidor = new Repartidor(Guid.NewGuid(), _tenant.UsuarioId!.Value, _tenant.OperadorId, "1.111.111-1", "099111111", _vehiculoId);
        _repartidores.Repartidores.Add(_repartidor);
    }

    private RegistrarPosicionesCommandHandler CrearHandler() => new(_repartidores, _rutas, _posiciones, _uow, _tenant, _clock);

    private static RegistrarPosicionesCommand Comando(params PosicionDispositivoDto[] posiciones) => new(posiciones);

    [Fact]
    public async Task PosicionSinRuta_SeAsociaAlVehiculoAsignadoDelRepartidor()
    {
        var resultado = await CrearHandler().Handle(Comando(new PosicionDispositivoDto(-34.9, -56.16, VelocidadKmh: 30, BateriaPct: 70)), default);

        Assert.Equal(1, resultado.Recibidas);
        var guardada = Assert.Single(_posiciones.Posiciones);
        Assert.Equal(_vehiculoId, guardada.VehiculoId);
        Assert.Equal(_repartidor.Id, guardada.RepartidorId);
        Assert.Equal(_tenant.OperadorId, guardada.OperadorId);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task LoteDiferido_ConservaElInstanteEnQueElDispositivoTomoCadaPosicion()
    {
        var hace10 = _clock.UtcNow.AddMinutes(-10);
        var hace5 = _clock.UtcNow.AddMinutes(-5);

        await CrearHandler().Handle(Comando(
            new PosicionDispositivoDto(-34.90, -56.10, hace10),
            new PosicionDispositivoDto(-34.91, -56.11, hace5)), default);

        Assert.Equal(new[] { hace10, hace5 }, _posiciones.Posiciones.Select(p => p.Timestamp));
    }

    [Fact]
    public async Task RelojAdelantadoDelDispositivo_NoColocaLaPosicionEnElFuturo()
    {
        await CrearHandler().Handle(Comando(new PosicionDispositivoDto(-34.9, -56.16, _clock.UtcNow.AddHours(2))), default);

        Assert.Equal(_clock.UtcNow, Assert.Single(_posiciones.Posiciones).Timestamp);
    }

    [Fact]
    public async Task ConRutaDelRepartidor_UsaElVehiculoDeLaRuta()
    {
        var vehiculoDeLaRuta = Guid.NewGuid();
        var ruta = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, DateOnly.FromDateTime(_clock.UtcNow));
        ruta.AsignarRecursos(_repartidor.Id, vehiculoDeLaRuta);
        _rutas.Rutas.Add(ruta);

        await CrearHandler().Handle(Comando(new PosicionDispositivoDto(-34.9, -56.16, HojaDeRutaId: ruta.Id)), default);

        var guardada = Assert.Single(_posiciones.Posiciones);
        Assert.Equal(vehiculoDeLaRuta, guardada.VehiculoId);
        Assert.Equal(ruta.Id, guardada.HojaDeRutaId);
    }

    [Fact]
    public async Task RutaDeOtroRepartidor_LanzaUnauthorizedYNoPersiste()
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, DateOnly.FromDateTime(_clock.UtcNow));
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        _rutas.Rutas.Add(ruta);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearHandler().Handle(Comando(new PosicionDispositivoDto(-34.9, -56.16, HojaDeRutaId: ruta.Id)), default));

        Assert.Empty(_posiciones.Posiciones);
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task UsuarioQueNoEsRepartidor_LanzaUnauthorized()
    {
        _repartidores.Repartidores.Clear();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearHandler().Handle(Comando(new PosicionDispositivoDto(-34.9, -56.16)), default));
    }

    [Fact]
    public async Task RepartidorSinVehiculoYSinRuta_LanzaDomainValidation()
    {
        _repartidores.Repartidores.Clear();
        _repartidores.Repartidores.Add(new Repartidor(Guid.NewGuid(), _tenant.UsuarioId!.Value, _tenant.OperadorId, "1.111.111-1", "099111111"));

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(new PosicionDispositivoDto(-34.9, -56.16)), default));
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public async Task CoordenadasFueraDeRango_LanzanDomainValidation(double latitud, double longitud)
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(new PosicionDispositivoDto(latitud, longitud)), default));

        Assert.Empty(_posiciones.Posiciones);
    }

    [Fact]
    public async Task LoteVacioOExcesivo_LanzaDomainValidation()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() => CrearHandler().Handle(Comando(), default));

        var excesivo = Enumerable.Range(0, RegistrarPosicionesCommandHandler.MaximoPorLote + 1)
            .Select(_ => new PosicionDispositivoDto(-34.9, -56.16)).ToArray();
        await Assert.ThrowsAsync<DomainValidationException>(() => CrearHandler().Handle(Comando(excesivo), default));
    }
}
