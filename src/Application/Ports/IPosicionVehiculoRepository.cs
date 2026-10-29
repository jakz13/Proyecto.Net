using UltimaMilla.Domain.Entities.Ejecucion;

namespace UltimaMilla.Application.Ports;

public interface IPosicionVehiculoRepository
{
    Task AddRangeAsync(IEnumerable<PosicionVehiculo> posiciones, CancellationToken cancellationToken = default);
}
