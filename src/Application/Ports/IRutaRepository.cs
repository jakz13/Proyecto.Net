using UltimaMilla.Domain.Entities.Planificacion;

namespace UltimaMilla.Application.Ports;

public interface IRutaRepository
{
    Task<HojaDeRuta?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<HojaDeRuta?> GetByIdWithParadasYAsignacionesAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Indica si el repartidor tiene otra hoja de ruta despachada que todavía no fue rendida en depósito.</summary>
    Task<bool> ExisteRutaSinRendirAsync(Guid repartidorId, Guid excluirHojaDeRutaId, CancellationToken cancellationToken = default);
    /// <summary>Hojas de ruta de una jornada del repartidor que estén en alguno de los estados indicados.</summary>
    Task<List<HojaDeRuta>> GetByRepartidorYFechaAsync(Guid repartidorId, DateOnly fecha, IReadOnlyCollection<string> estados, CancellationToken cancellationToken = default);
    /// <summary>Devuelve la otra hoja de ruta despachada del repartidor que todavía no fue rendida, si existe.</summary>
    Task<HojaDeRuta?> GetRutaSinRendirAsync(Guid repartidorId, Guid excluirHojaDeRutaId, CancellationToken cancellationToken = default);
    /// <summary>Lista las hojas de ruta de un operador con filtros opcionales por fecha, repartidor y estados.</summary>
    Task<List<HojaDeRuta>> ListarAsync(Guid operadorId, DateOnly? fecha, Guid? repartidorId, IReadOnlyCollection<string>? estados, CancellationToken cancellationToken = default);
    Task AddAsync(HojaDeRuta hojaDeRuta, CancellationToken cancellationToken = default);
    Task UpdateAsync(HojaDeRuta hojaDeRuta, CancellationToken cancellationToken = default);
    Task<List<HojaDeRuta>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        Guid operadorId,
        CancellationToken cancellationToken = default);
}
