using UltimaMilla.Domain.Entities.Planificacion;

namespace UltimaMilla.Application.Ports;

public interface IRutaRepository
{
    Task<HojaDeRuta?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<HojaDeRuta?> GetByIdWithParadasYAsignacionesAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Indica si el repartidor tiene otra hoja de ruta despachada que todavía no fue rendida en depósito.</summary>
    Task<bool> ExisteRutaSinRendirAsync(Guid repartidorId, Guid excluirHojaDeRutaId, CancellationToken cancellationToken = default);
    Task AddAsync(HojaDeRuta hojaDeRuta, CancellationToken cancellationToken = default);
    Task UpdateAsync(HojaDeRuta hojaDeRuta, CancellationToken cancellationToken = default);
}
