using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Incidencias;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public class EfIncidenciaRepository : IIncidenciaRepository
{
    private readonly AppDbContext _dbContext;

    public EfIncidenciaRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Incidencia incidencia, CancellationToken cancellationToken = default)
    {
        await _dbContext.Incidencias.AddAsync(incidencia, cancellationToken);
    }

    public async Task<Incidencia?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Incidencias.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<(List<Incidencia> Items, int Total)> ListarAsync(
        Guid operadorId, string? estado, string? tipo, int pagina, int tamanoPagina, CancellationToken cancellationToken = default)
    {
        var consulta = _dbContext.Incidencias.AsNoTracking().Where(i => i.OperadorId == operadorId);

        if (estado is not null)
            consulta = consulta.Where(i => i.Estado == estado);
        if (tipo is not null)
            consulta = consulta.Where(i => i.Tipo == tipo);

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderByDescending(i => i.FechaApertura)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
