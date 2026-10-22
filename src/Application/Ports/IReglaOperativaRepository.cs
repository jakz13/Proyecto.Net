using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Application.Ports;

public interface IReglaOperativaRepository
{
    Task AddAsync(ReglaOperativa regla, CancellationToken cancellationToken = default);
    Task<ReglaOperativa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Devuelve la versión vigente (sin VigenteHasta) del operador, con seguimiento de cambios.</summary>
    Task<ReglaOperativa?> ObtenerVigenteAsync(Guid operadorId, CancellationToken cancellationToken = default);
    /// <summary>Lista todas las versiones del operador, la más reciente primero.</summary>
    Task<List<ReglaOperativa>> ListarAsync(Guid operadorId, CancellationToken cancellationToken = default);
}