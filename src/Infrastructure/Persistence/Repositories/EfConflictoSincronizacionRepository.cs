using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Incidencias;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Retiros;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

/// <summary>
/// Lectura multitenant de conflictos de sincronización offline resueltos: une cada conflicto con su envío o retiro
/// para filtrar estrictamente por <paramref name="operadorId"/> y exponer el código de seguimiento legible.
/// </summary>
public class EfConflictoSincronizacionRepository : IConflictoSincronizacionRepository
{
    private readonly AppDbContext _dbContext;

    public EfConflictoSincronizacionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ConflictoSincronizacionDto>> ListarRecientesPorOperadorAsync(
        Guid operadorId,
        int maximo,
        CancellationToken cancellationToken = default)
    {
        if (operadorId == Guid.Empty || maximo <= 0)
            return Array.Empty<ConflictoSincronizacionDto>();

        var deEnvios = await (
            from c in _dbContext.ConflictosSincronizacion.AsNoTracking()
            join e in _dbContext.Envios.IgnoreQueryFilters().AsNoTracking() on c.EntidadId equals e.Id
            where c.EntidadAfectada == nameof(Envio) && e.OperadorId == operadorId
            orderby c.Timestamp descending
            select new ConflictoSincronizacionDto(
                c.Id,
                c.Timestamp,
                c.EntidadAfectada,
                c.EntidadId,
                e.CodigoSeguimientoPublico,
                c.EstadoDispositivo,
                c.EstadoServidor,
                c.PoliticaAplicada,
                c.Resolucion))
            .Take(maximo)
            .ToListAsync(cancellationToken);

        var deRetiros = await (
            from c in _dbContext.ConflictosSincronizacion.AsNoTracking()
            join r in _dbContext.Retiros.IgnoreQueryFilters().AsNoTracking() on c.EntidadId equals r.Id
            where c.EntidadAfectada == nameof(Retiro) && r.OperadorId == operadorId
            orderby c.Timestamp descending
            select new ConflictoSincronizacionDto(
                c.Id,
                c.Timestamp,
                c.EntidadAfectada,
                c.EntidadId,
                r.CodigoSeguimientoPublico,
                c.EstadoDispositivo,
                c.EstadoServidor,
                c.PoliticaAplicada,
                c.Resolucion))
            .Take(maximo)
            .ToListAsync(cancellationToken);

        return deEnvios
            .Concat(deRetiros)
            .OrderByDescending(c => c.Timestamp)
            .Take(maximo)
            .ToList();
    }
}
