using UltimaMilla.Domain.Entities.Ejecucion;

namespace UltimaMilla.Application.Ports;

/// <summary>Rendiciones de repartidores en depósito. No confirma cambios: lo hace IUnitOfWork.</summary>
public interface IRendicionRepository
{
    Task AddAsync(RendicionDeposito rendicion, CancellationToken cancellationToken = default);
    Task<RendicionDeposito?> GetByHojaDeRutaIdAsync(Guid hojaDeRutaId, CancellationToken cancellationToken = default);
}
