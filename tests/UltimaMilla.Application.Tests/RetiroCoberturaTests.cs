using UltimaMilla.Application.Commands.AltaEnvio;
using UltimaMilla.Application.Commands.Retiros.SolicitarRetiro;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Services;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Retiros;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

/// <summary>El domicilio de un retiro pedido por el comercio debe caer dentro de las zonas de cobertura del operador.</summary>
public class RetiroCoberturaTests
{
    private const string Cuadrado = "[[-34.90, -56.20], [-34.90, -56.15], [-34.92, -56.15], [-34.92, -56.20], [-34.90, -56.20]]";

    private readonly Guid _operador = Guid.NewGuid();
    private readonly Guid _comercio = Guid.NewGuid();
    private readonly FakeClock _clock = new();
    private readonly FakeDireccionRepository _direcciones = new();
    private readonly FakeCuadroTarifarioRepository _cuadros = new();
    private readonly FakeZonaCoberturaRepository _zonas = new();
    private readonly RetiroRepositorioEnMemoria _retiros = new();
    private readonly ZonaCobertura _centro;

    public RetiroCoberturaTests()
    {
        _centro = new ZonaCobertura(Guid.NewGuid(), _operador, "Centro", Cuadrado);
        _zonas.Zonas.Add(_centro);

        var cuadro = new CuadroTarifario(Guid.NewGuid(), _operador, 1, _clock.UtcNow.AddDays(-30));
        cuadro.AgregarRegla(new ReglaTarifa(Guid.NewGuid(), cuadro.Id, SolicitarRetiroProcesador.ModalidadRetiro, 0, 10m, 0, 1000m, 100m, 0m, 0m, null));
        _cuadros.Cuadros.Add(cuadro);
    }

    private SolicitarRetiroProcesador Procesador(double latitud, double longitud) =>
        new(_retiros, _direcciones, _cuadros, _clock, null,
            new VerificadorCobertura(_zonas, new GeocodificadorFijo(latitud, longitud)));

    private SolicitarRetiroCommand Comando() => new(
        _comercio,
        new ContactoRetiroDto("Ana Gomez"),
        new DireccionAltaDto("18 de Julio", "1234", "Montevideo"),
        new List<BultoRetiroAltaDto> { new("Caja", 2m, 10m, 10m, 10m) },
        "Devolución");

    [Fact]
    public async Task Retiro_FueraDeLasZonasDelOperador_SeRechazaYNoSeGuardaNada()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() => Procesador(-34.60, -56.00).ProcesarAsync(_operador, Comando(), default));

        Assert.Empty(_retiros.Retiros);
        Assert.Empty(_direcciones.Direcciones);
    }

    [Fact]
    public async Task Retiro_DentroDeUnaZona_SeAdmiteYLaDireccionQuedaConSuZona()
    {
        var resultado = await Procesador(-34.91, -56.17).ProcesarAsync(_operador, Comando(), default);

        Assert.False(resultado.Duplicado);
        Assert.Single(_retiros.Retiros);
        Assert.Equal(_centro.Id, _direcciones.Direcciones.Single().ZonaCoberturaId);
    }

    private sealed class GeocodificadorFijo(double latitud, double longitud) : IGeocodificador
    {
        public Task<(double Latitud, double Longitud)?> GeocodificarAsync(string calle, string numero, string ciudad, CancellationToken cancellationToken = default)
            => Task.FromResult<(double Latitud, double Longitud)?>((latitud, longitud));
    }

    private sealed class RetiroRepositorioEnMemoria : IRetiroRepository
    {
        public List<Retiro> Retiros { get; } = new();

        public Task AddAsync(Retiro retiro, CancellationToken cancellationToken = default) { Retiros.Add(retiro); return Task.CompletedTask; }
        public Task<Retiro?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Retiros.FirstOrDefault(r => r.Id == id));
        public Task<List<Retiro>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default) => Task.FromResult(Retiros.Where(r => ids.Contains(r.Id)).ToList());
        public Task<Retiro?> GetByCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default) => Task.FromResult<Retiro?>(null);
        public Task<bool> ExisteCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<Retiro?> GetByReferenciaExternaAsync(Guid comercioOperadorId, string referenciaExterna, CancellationToken cancellationToken = default) => Task.FromResult<Retiro?>(null);
        public Task<(List<Retiro> Items, int Total)> BuscarPorComercioAsync(Guid operadorId, Guid comercioOperadorId, EstadoRetiro? estado, int pagina, int tamanoPagina, CancellationToken cancellationToken = default) => Task.FromResult((new List<Retiro>(), 0));
        public Task<List<Retiro>> ListarPorOperadorAsync(Guid operadorId, EstadoRetiro? estado, CancellationToken cancellationToken = default) => Task.FromResult(new List<Retiro>());
        public Task<IReadOnlyDictionary<EstadoRetiro, int>> ContarPorEstadoAsync(Guid operadorId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<EstadoRetiro, int>>(new Dictionary<EstadoRetiro, int>());
        public Task UpdateAsync(Retiro retiro, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
