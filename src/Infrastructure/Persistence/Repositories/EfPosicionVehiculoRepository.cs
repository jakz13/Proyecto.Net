using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Ejecucion;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public class EfPosicionVehiculoRepository : IPosicionVehiculoRepository
{
    private readonly AppDbContext _dbContext;

    public EfPosicionVehiculoRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddRangeAsync(IEnumerable<PosicionVehiculo> posiciones, CancellationToken cancellationToken = default)
    {
        await _dbContext.PosicionesVehiculo.AddRangeAsync(posiciones, cancellationToken);
    }
}
