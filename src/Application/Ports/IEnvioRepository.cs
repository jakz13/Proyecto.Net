using UltimaMilla.Domain.Entities.Envios;

namespace UltimaMilla.Application.Ports;

public interface IEnvioRepository
{
    Task AddAsync(Envio envio, CancellationToken cancellationToken = default);
    Task<List<Envio>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Envio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Envio>> GetByHojaDeRutaIdAsync(Guid hojaDeRutaId, CancellationToken cancellationToken = default);
    Task<List<Envio>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task UpdateAsync(Envio envio, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<Envio> envios, CancellationToken cancellationToken = default);
}
