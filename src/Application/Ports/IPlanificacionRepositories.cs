using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Application.Ports;

public interface IRepartidorRepository
{
    Task<Repartidor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IVehiculoRepository
{
    Task<Vehiculo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IFranjaHorariaRepository
{
    Task<List<FranjaHoraria>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
