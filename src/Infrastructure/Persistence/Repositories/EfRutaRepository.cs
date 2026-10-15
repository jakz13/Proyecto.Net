using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Planificacion;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public class EfRutaRepository : IRutaRepository
{
    private readonly AppDbContext _dbContext;

    public EfRutaRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HojaDeRuta?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.HojasDeRuta
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task<HojaDeRuta?> GetByIdWithParadasYAsignacionesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.HojasDeRuta
            .Include(h => h.Paradas)
            .Include(h => h.Asignaciones)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public Task UpdateAsync(HojaDeRuta hojaDeRuta, CancellationToken cancellationToken = default)
    {
        _dbContext.HojasDeRuta.Update(hojaDeRuta);
        return Task.CompletedTask;
    }
}
