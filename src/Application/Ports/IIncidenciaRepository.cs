using UltimaMilla.Domain.Entities.Incidencias;

namespace UltimaMilla.Application.Ports;

public interface IIncidenciaRepository
{
    Task AddAsync(Incidencia incidencia, CancellationToken cancellationToken = default);
    Task<Incidencia?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Lista las incidencias del operador, las más recientes primero. Estado y tipo son filtros opcionales.</summary>
    Task<(List<Incidencia> Items, int Total)> ListarAsync(Guid operadorId, string? estado, string? tipo, int pagina, int tamanoPagina, CancellationToken cancellationToken = default);
}
