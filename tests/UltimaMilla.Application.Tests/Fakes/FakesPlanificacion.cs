using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;

namespace UltimaMilla.Application.Tests.Fakes;

/// <summary>Repositorio de hojas de ruta en memoria para tests de handlers.</summary>
public class FakeRutaRepository : IRutaRepository
{
    public List<HojaDeRuta> Rutas { get; } = new();

    public Task<HojaDeRuta?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rutas.FirstOrDefault(r => r.Id == id));

    public Task<HojaDeRuta?> GetByIdWithParadasYAsignacionesAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rutas.FirstOrDefault(r => r.Id == id));

    public Task AddAsync(HojaDeRuta hojaDeRuta, CancellationToken cancellationToken = default)
    {
        Rutas.Add(hojaDeRuta);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(HojaDeRuta hojaDeRuta, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class FakeRepartidorRepository : IRepartidorRepository
{
    public List<Repartidor> Repartidores { get; } = new();

    public Task<Repartidor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Repartidores.FirstOrDefault(r => r.Id == id));
}

public class FakeVehiculoRepository : IVehiculoRepository
{
    public List<Vehiculo> Vehiculos { get; } = new();

    public Task<Vehiculo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Vehiculos.FirstOrDefault(v => v.Id == id));
}

public class FakeFranjaHorariaRepository : IFranjaHorariaRepository
{
    public List<FranjaHoraria> Franjas { get; } = new();

    public Task<List<FranjaHoraria>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var set = ids.ToHashSet();
        return Task.FromResult(Franjas.Where(f => set.Contains(f.Id)).ToList());
    }
}
