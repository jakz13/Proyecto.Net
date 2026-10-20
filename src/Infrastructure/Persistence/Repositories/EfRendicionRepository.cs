using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Ejecucion;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public class EfRendicionRepository : IRendicionRepository
{
    private readonly AppDbContext _dbContext;

    public EfRendicionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(RendicionDeposito rendicion, CancellationToken cancellationToken = default)
    {
        await _dbContext.RendicionesDeposito.AddAsync(rendicion, cancellationToken);
    }

    public async Task<RendicionDeposito?> GetByHojaDeRutaIdAsync(Guid hojaDeRutaId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RendicionesDeposito
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.HojaDeRutaId == hojaDeRutaId, cancellationToken);
    }
}
