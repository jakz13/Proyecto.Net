using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Ejecucion;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public class EfDevolucionRepository : IDevolucionRepository
{
    private readonly AppDbContext _dbContext;

    public EfDevolucionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Devolucion devolucion, CancellationToken cancellationToken = default)
    {
        await _dbContext.Devoluciones.AddAsync(devolucion, cancellationToken);
    }

    public async Task<Devolucion?> GetByEnvioIdAsync(Guid envioId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Devoluciones.FirstOrDefaultAsync(d => d.EnvioId == envioId, cancellationToken);
    }

    public async Task<List<Devolucion>> GetByEnviosIdsAsync(IEnumerable<Guid> enviosIds, CancellationToken cancellationToken = default)
    {
        var ids = enviosIds.ToHashSet();
        return await _dbContext.Devoluciones.AsNoTracking().Where(d => ids.Contains(d.EnvioId)).ToListAsync(cancellationToken);
    }
}
