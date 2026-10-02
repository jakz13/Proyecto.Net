using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities;

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
}
