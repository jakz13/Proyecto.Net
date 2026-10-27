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

    public Task<bool> ExisteRutaSinRendirAsync(Guid repartidorId, Guid excluirHojaDeRutaId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rutas.Any(r => r.RepartidorId == repartidorId
                                       && r.Id != excluirHojaDeRutaId
                                       && r.Estado is "Despachada" or "EnTransito" or "Finalizada"));

    public Task<HojaDeRuta?> GetRutaSinRendirAsync(Guid repartidorId, Guid excluirHojaDeRutaId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rutas.FirstOrDefault(r => r.RepartidorId == repartidorId
                                                  && r.Id != excluirHojaDeRutaId
                                                  && r.Estado is "Despachada" or "EnTransito" or "Finalizada"));

    public Task<List<HojaDeRuta>> ListarAsync(Guid operadorId, DateOnly? fecha, Guid? repartidorId, IReadOnlyCollection<string>? estados, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rutas
            .Where(r => r.OperadorId == operadorId
                        && (!fecha.HasValue || r.Fecha == fecha.Value)
                        && (!repartidorId.HasValue || r.RepartidorId == repartidorId.Value)
                        && (estados is null || estados.Count == 0 || estados.Contains(r.Estado)))
            .ToList());

    public Task<List<HojaDeRuta>> GetByRepartidorYFechaAsync(Guid repartidorId, DateOnly fecha, IReadOnlyCollection<string> estados, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rutas.Where(r => r.RepartidorId == repartidorId && r.Fecha == fecha && estados.Contains(r.Estado)).ToList());

    public Task AddAsync(HojaDeRuta hojaDeRuta, CancellationToken cancellationToken = default)
    {
        Rutas.Add(hojaDeRuta);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(HojaDeRuta hojaDeRuta, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<List<HojaDeRuta>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        Guid operadorId,
        CancellationToken cancellationToken = default)
    {
        var idsBuscados = ids.ToHashSet();

        return Task.FromResult(Rutas
            .Where(r => r.OperadorId == operadorId &&
                        idsBuscados.Contains(r.Id))
            .ToList());
    }
}

public class FakeRepartidorRepository : IRepartidorRepository
{
    public List<Repartidor> Repartidores { get; } = new();

    public Task<Repartidor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Repartidores.FirstOrDefault(r => r.Id == id));

    public Task<List<Repartidor>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var set = ids.ToHashSet();
        return Task.FromResult(Repartidores.Where(r => set.Contains(r.Id)).ToList());
    }

    public Task<Repartidor?> GetByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Repartidores.FirstOrDefault(r => r.UsuarioId == usuarioId));
}

public class FakeVehiculoRepository : IVehiculoRepository
{
    public List<Vehiculo> Vehiculos { get; } = new();

    public Task<Vehiculo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Vehiculos.FirstOrDefault(v => v.Id == id));

    public Task<List<Vehiculo>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var set = ids.ToHashSet();
        return Task.FromResult(Vehiculos.Where(v => set.Contains(v.Id)).ToList());
    }
}

public class FakeFranjaHorariaRepository : IFranjaHorariaRepository
{
    public List<FranjaHoraria> Franjas { get; } = new();

    public Task<List<FranjaHoraria>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var set = ids.ToHashSet();
        return Task.FromResult(Franjas.Where(f => set.Contains(f.Id)).ToList());
    }

    public Task<List<FranjaHoraria>> ListarActivasPorZonaAsync(Guid zonaCoberturaId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Franjas.Where(f => f.ZonaCoberturaId == zonaCoberturaId && f.Activa).OrderBy(f => f.DiaSemana).ThenBy(f => f.HoraInicio).ToList());
}
