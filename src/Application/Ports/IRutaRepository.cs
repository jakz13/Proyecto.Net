using UltimaMilla.Domain.Entities.Planificacion;

namespace UltimaMilla.Application.Ports;

public interface IRutaRepository
{
    Task<HojaDeRuta?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<HojaDeRuta?> GetByIdWithParadasYAsignacionesAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(HojaDeRuta hojaDeRuta, CancellationToken cancellationToken = default);
    Task UpdateAsync(HojaDeRuta hojaDeRuta, CancellationToken cancellationToken = default);
}
