using UltimaMilla.Domain.Entities.Envios;

namespace UltimaMilla.Application.Ports;

public interface IEnvioRepository
{
    Task AddAsync(Envio envio, CancellationToken cancellationToken = default);
    Task<List<Envio>> GetAllAsync(CancellationToken cancellationToken = default);
}
