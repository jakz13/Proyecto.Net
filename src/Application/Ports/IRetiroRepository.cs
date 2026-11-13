using UltimaMilla.Domain.Entities.Retiros;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Ports;

/// <summary>
/// Acceso a datos del retiro en domicilio (RF 37 a RF 41). No confirma cambios: lo hace IUnitOfWork.
/// Las lecturas con inquilino quedan acotadas al operador por el filtro global; las que no tienen sesión
/// (seguimiento público, clave de API) lo omiten de forma explícita y se acotan por código o por comercio.
/// </summary>
public interface IRetiroRepository
{
    Task AddAsync(Retiro retiro, CancellationToken cancellationToken = default);

    Task<Retiro?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<Retiro>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Consulta pública y anónima: el código identifica al retiro en cualquier operador.</summary>
    Task<Retiro?> GetByCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default);

    Task<bool> ExisteCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default);

    /// <summary>Lo usa el canal por clave de API, sin inquilino: el alcance lo da el comercio explícito.</summary>
    Task<Retiro?> GetByReferenciaExternaAsync(Guid comercioOperadorId, string referenciaExterna, CancellationToken cancellationToken = default);

    Task<(List<Retiro> Items, int Total)> BuscarPorComercioAsync(
        Guid operadorId,
        Guid comercioOperadorId,
        EstadoRetiro? estado,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default);

    Task<List<Retiro>> ListarPorOperadorAsync(Guid operadorId, EstadoRetiro? estado, CancellationToken cancellationToken = default);

    /// <summary>Cantidad de retiros del operador por estado, para el tablero de operación y los reportes (RF 41).</summary>
    Task<IReadOnlyDictionary<EstadoRetiro, int>> ContarPorEstadoAsync(Guid operadorId, CancellationToken cancellationToken = default);

    /// <summary>Retiros en estado Solicitado o AsignadoARuta del comercio para cancelar al darlo de baja.</summary>
    Task<List<Retiro>> ListarParaBajaComercioAsync(Guid operadorId, Guid comercioOperadorId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Retiro retiro, CancellationToken cancellationToken = default);
}
