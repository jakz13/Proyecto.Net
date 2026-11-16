using UltimaMilla.Application.Queries.Incidencias;

namespace UltimaMilla.Application.Ports;

/// <summary>
/// Puerto de lectura de conflictos de sincronización offline resueltos.
/// Acota siempre por <paramref name="operadorId"/> uniendo cada conflicto con el envío o retiro dueño de la entidad.
/// </summary>
public interface IConflictoSincronizacionRepository
{
    Task<IReadOnlyList<ConflictoSincronizacionDto>> ListarRecientesPorOperadorAsync(
        Guid operadorId,
        int maximo,
        CancellationToken cancellationToken = default);
}
