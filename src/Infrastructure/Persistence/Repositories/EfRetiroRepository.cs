using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Retiros;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public class EfRetiroRepository : IRetiroRepository
{
    private readonly AppDbContext _dbContext;

    public EfRetiroRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Retiro retiro, CancellationToken cancellationToken = default)
    {
        await _dbContext.Retiros.AddAsync(retiro, cancellationToken);
    }

    public async Task<Retiro?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Retiros.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<List<Retiro>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idSet = ids.ToHashSet();
        if (idSet.Count == 0)
            return new List<Retiro>();

        return await _dbContext.Retiros.Where(r => idSet.Contains(r.Id)).ToListAsync(cancellationToken);
    }

    public async Task<Retiro?> GetByCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default)
    {
        var codigo = codigoSeguimiento.Trim().ToUpperInvariant();
        // Consulta pública y anónima: el código de seguimiento identifica al retiro en cualquier operador
        return await _dbContext.Retiros
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CodigoSeguimientoPublico == codigo, cancellationToken);
    }

    public async Task<bool> ExisteCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default)
    {
        var codigo = codigoSeguimiento.Trim().ToUpperInvariant();
        return await _dbContext.Retiros.IgnoreQueryFilters().AnyAsync(r => r.CodigoSeguimientoPublico == codigo, cancellationToken);
    }

    public async Task<Retiro?> GetByReferenciaExternaAsync(Guid comercioOperadorId, string referenciaExterna, CancellationToken cancellationToken = default)
    {
        // Lo invoca el canal por clave de API, sin inquilino: el alcance lo da el comercio explícito
        return await _dbContext.Retiros
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ComercioOperadorId == comercioOperadorId && r.ReferenciaExterna == referenciaExterna, cancellationToken);
    }

    public async Task<(List<Retiro> Items, int Total)> BuscarPorComercioAsync(
        Guid operadorId,
        Guid comercioOperadorId,
        EstadoRetiro? estado,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default)
    {
        var consulta = _dbContext.Retiros
            .AsNoTracking()
            .Where(r => r.OperadorId == operadorId && r.ComercioOperadorId == comercioOperadorId);

        if (estado.HasValue)
            consulta = consulta.Where(r => r.EstadoActual == estado.Value);

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderByDescending(r => r.FechaSolicitud)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<List<Retiro>> ListarPorOperadorAsync(Guid operadorId, EstadoRetiro? estado, CancellationToken cancellationToken = default)
    {
        var consulta = _dbContext.Retiros.AsNoTracking().Where(r => r.OperadorId == operadorId);

        if (estado.HasValue)
            consulta = consulta.Where(r => r.EstadoActual == estado.Value);

        return await consulta.OrderBy(r => r.FechaSolicitud).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<EstadoRetiro, int>> ContarPorEstadoAsync(Guid operadorId, CancellationToken cancellationToken = default)
    {
        var filas = await _dbContext.Retiros
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Where(r => r.OperadorId == operadorId)
            .GroupBy(r => r.EstadoActual)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToListAsync(cancellationToken);

        return filas.ToDictionary(f => f.Estado, f => f.Cantidad);
    }

    public async Task<List<Retiro>> ListarParaBajaComercioAsync(Guid operadorId, Guid comercioOperadorId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Retiros
            .Where(r => r.OperadorId == operadorId
                        && r.ComercioOperadorId == comercioOperadorId
                        && (r.EstadoActual == EstadoRetiro.Solicitado || r.EstadoActual == EstadoRetiro.AsignadoARuta))
            .ToListAsync(cancellationToken);
    }

    public Task UpdateAsync(Retiro retiro, CancellationToken cancellationToken = default)
    {
        // Una entidad ya rastreada detecta sola sus cambios; Update marcaría como modificados los hijos nuevos (eventos) y fallaría al guardar
        if (_dbContext.Entry(retiro).State == EntityState.Detached)
            _dbContext.Retiros.Update(retiro);

        return Task.CompletedTask;
    }
}
