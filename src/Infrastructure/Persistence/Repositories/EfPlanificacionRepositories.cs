using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public class EfRepartidorRepository : IRepartidorRepository
{
    private readonly AppDbContext _dbContext;

    public EfRepartidorRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Repartidor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Repartidores.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }
}

public class EfVehiculoRepository : IVehiculoRepository
{
    private readonly AppDbContext _dbContext;

    public EfVehiculoRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Vehiculo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Vehiculos.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }
}

public class EfFranjaHorariaRepository : IFranjaHorariaRepository
{
    private readonly AppDbContext _dbContext;

    public EfFranjaHorariaRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<FranjaHoraria>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idSet = ids.ToHashSet();
        return await _dbContext.FranjasHorarias
            .AsNoTracking()
            .Where(f => idSet.Contains(f.Id))
            .ToListAsync(cancellationToken);
    }
}
