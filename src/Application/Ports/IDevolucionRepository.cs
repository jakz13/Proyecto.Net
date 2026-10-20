using UltimaMilla.Domain.Entities.Ejecucion;

namespace UltimaMilla.Application.Ports;

/// <summary>Devoluciones de envíos al comercio. No confirma cambios: lo hace IUnitOfWork.</summary>
public interface IDevolucionRepository
{
    Task AddAsync(Devolucion devolucion, CancellationToken cancellationToken = default);
    Task<Devolucion?> GetByEnvioIdAsync(Guid envioId, CancellationToken cancellationToken = default);
    Task<List<Devolucion>> GetByEnviosIdsAsync(IEnumerable<Guid> enviosIds, CancellationToken cancellationToken = default);
}
