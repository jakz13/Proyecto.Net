using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public class EfEnvioRepository : IEnvioRepository
{
    private readonly AppDbContext _dbContext;

    public EfEnvioRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Envio envio, CancellationToken cancellationToken = default)
    {
        await _dbContext.Envios.AddAsync(envio, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Envio>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Envios
            .AsNoTracking()
            .OrderByDescending(e => e.FechaCreacion)
            .ToListAsync(cancellationToken);
    }

    public async Task<Envio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Envios
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<List<Envio>> GetByHojaDeRutaIdAsync(Guid hojaDeRutaId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Envios
            .Where(e => e.HojaDeRutaId == hojaDeRutaId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Envio>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idSet = ids.ToHashSet();
        return await _dbContext.Envios
            .Where(e => idSet.Contains(e.Id))
            .ToListAsync(cancellationToken);
    }

    public Task UpdateAsync(Envio envio, CancellationToken cancellationToken = default)
    {
        _dbContext.Envios.Update(envio);
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IEnumerable<Envio> envios, CancellationToken cancellationToken = default)
    {
        _dbContext.Envios.UpdateRange(envios);
        return Task.CompletedTask;
    }
}
