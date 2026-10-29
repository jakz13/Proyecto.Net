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

    /// <summary>
    /// Importa masivamente órdenes de envío a partir de un archivo CSV con cálculo de tarifas e idempotencia (UC-25, RF-07).
    /// </summary>
    Task<ResultadoImportacionDto> ImportarEnviosPorArchivoAsync(
        byte[] contenidoArchivo,
        string nombreArchivo,
        string? claveIdempotencia = null,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene la clave de API vigente en la sesión actual del comercio para invocar el canal de integración.
    /// </summary>
    Task<string> ObtenerClaveApiActivaAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Genera una nueva clave de API para el comercio, invalidando la credencial anterior.
    /// </summary>
    Task<ClaveApiComercioDto> RegenerarClaveApiAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Registra un envío invocando el endpoint de integración externa autenticado mediante X-Api-Key.
    /// </summary>
    Task<RespuestaAltaEnvioApiDto> CrearEnvioPorApiIntegracionAsync(
        SolicitudAltaEnvioApiDto solicitud,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Genera las etiquetas físicas e imprimibles para uno o más envíos seleccionados en estado Admitido (UC-27).
    /// </summary>
    Task<ResultadoGeneracionEtiquetasDto> GenerarEtiquetasEnviosAsync(
        IReadOnlyList<Guid> envioIds,
        CancellationToken cancelacion = default);
}
