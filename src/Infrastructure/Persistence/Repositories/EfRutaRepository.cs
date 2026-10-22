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

    public async Task<List<HojaDeRuta>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idSet = ids.ToHashSet();
        if (idSet.Count == 0)
            return new List<HojaDeRuta>();

        return await _dbContext.HojasDeRuta
            .AsNoTracking()
            .Where(h => idSet.Contains(h.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<HojaDeRuta?> GetByIdWithParadasYAsignacionesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.HojasDeRuta
            .Include(h => h.Paradas)
            .Include(h => h.Asignaciones)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task<bool> ExisteRutaSinRendirAsync(Guid repartidorId, Guid excluirHojaDeRutaId, CancellationToken cancellationToken = default)
    {
        // Despachada o finalizada en calle, pero aún sin rendición en depósito
        return await _dbContext.HojasDeRuta.AnyAsync(
            h => h.RepartidorId == repartidorId
                 && h.Id != excluirHojaDeRutaId
                 && (h.Estado == "Despachada" || h.Estado == "EnTransito" || h.Estado == "Finalizada"),
            cancellationToken);
    }

    public async Task<List<HojaDeRuta>> GetByRepartidorYFechaAsync(Guid repartidorId, DateOnly fecha, IReadOnlyCollection<string> estados, CancellationToken cancellationToken = default)
    {
        return await _dbContext.HojasDeRuta
            .AsNoTracking()
            .Where(h => h.RepartidorId == repartidorId && h.Fecha == fecha && estados.Contains(h.Estado))
            .OrderBy(h => h.FechaLiberacion)
            .ToListAsync(cancellationToken);
    }

    public async Task<HojaDeRuta?> GetRutaSinRendirAsync(Guid repartidorId, Guid excluirHojaDeRutaId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.HojasDeRuta
            .AsNoTracking()
            .Where(h => h.RepartidorId == repartidorId
                        && h.Id != excluirHojaDeRutaId
                        && (h.Estado == "Despachada" || h.Estado == "EnTransito" || h.Estado == "Finalizada"))
            .OrderByDescending(h => h.FechaDespacho)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<HojaDeRuta>> ListarAsync(Guid operadorId, DateOnly? fecha, Guid? repartidorId, IReadOnlyCollection<string>? estados, CancellationToken cancellationToken = default)
    {
        var consulta = _dbContext.HojasDeRuta.AsNoTracking().Where(h => h.OperadorId == operadorId);

        if (fecha.HasValue)
            consulta = consulta.Where(h => h.Fecha == fecha.Value);

        if (repartidorId.HasValue)
            consulta = consulta.Where(h => h.RepartidorId == repartidorId.Value);

        if (estados is { Count: > 0 })
            consulta = consulta.Where(h => estados.Contains(h.Estado));

        return await consulta
            .OrderByDescending(h => h.Fecha)
            .ThenByDescending(h => h.FechaLiberacion)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(HojaDeRuta hojaDeRuta, CancellationToken cancellationToken = default)
    {
        await _dbContext.HojasDeRuta.AddAsync(hojaDeRuta, cancellationToken);
    }

    public Task UpdateAsync(HojaDeRuta hojaDeRuta, CancellationToken cancellationToken = default)
    {
        // Una entidad ya rastreada detecta sola sus cambios; Update marcaría como modificados los hijos nuevos y fallaría al guardar
        if (_dbContext.Entry(hojaDeRuta).State == EntityState.Detached)
            _dbContext.HojasDeRuta.Update(hojaDeRuta);

        return Task.CompletedTask;
    }
}
