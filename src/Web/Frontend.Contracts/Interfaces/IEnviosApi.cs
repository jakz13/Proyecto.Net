using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Envios;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de servicio para operaciones sobre órdenes de envío.
/// Consumido por el Portal del Comercio (UC-24, UC-28) y Backoffice.
/// </summary>
public interface IEnviosApi
{
    /// <summary>
    /// Consulta el listado de envíos aplicando filtros de estado, fechas y paginación (UC-28).
    /// </summary>
    Task<ResultadoPaginadoDto<EnvioDto>> ListarEnviosAsync(FiltroEnviosDto filtro, CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene el detalle de un envío particular por identificador.
    /// </summary>
    Task<EnvioDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default);

    /// <summary>
    /// Registra una nueva orden de envío individual (UC-24).
    /// El comercio solicitante y el operador son resueltos exclusivamente por el token JWT.
    /// </summary>
    Task<EnvioDto> CrearEnvioAsync(CrearEnvioDto nuevoEnvio, CancellationToken cancelacion = default);

    /// <summary>
    /// Declara formalmente un envío en depósito o en tránsito como extraviado (UC-43, RF-11, RF-12).
    /// </summary>
    Task<ResultadoDeclaracionExtravioDto> DeclararEnvioExtraviadoAsync(DeclararExtraviadoDto dto, CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene la trazabilidad y registro histórico completo de eventos de un envío.
    /// </summary>
    Task<IReadOnlyList<EventoHistorialEnvioDto>> ObtenerHistorialEnvioAsync(Guid envioId, CancellationToken cancelacion = default);

    // TODO: Definir contrato para importación masiva de envíos (UC-25) enviando cabecera Idempotency-Key (SHA-256 del archivo).
    // TODO: Definir contrato para generación y descarga de etiquetas en PDF/ZPL (UC-26).
}
