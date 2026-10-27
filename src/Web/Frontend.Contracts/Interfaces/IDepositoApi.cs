using UltimaMilla.Frontend.Contracts.Modelos.Deposito;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de servicio para operaciones del operario en depósito (UC-14, UC-15, RF-10).
/// </summary>
public interface IDepositoApi
{
    /// <summary>
    /// Lista los envíos en estado Admitido listos para ser recepcionados en depósito (UC-14).
    /// </summary>
    Task<IReadOnlyList<EnvioAdmitidoDto>> ListarEnviosAdmitidosAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene el detalle de un envío admitido por ID o código de seguimiento para su recepción.
    /// </summary>
    Task<EnvioAdmitidoDto?> ObtenerEnvioParaRecepcionAsync(Guid envioId, CancellationToken cancelacion = default);

    /// <summary>
    /// Contrasta los bultos escaneados contra los declarados y ejecuta la recepción en depósito (UC-14, RF-10).
    /// </summary>
    Task<ResultadoRecepcionDto> RecepcionarBultosAsync(Guid envioId, RecepcionarBultosRequestDto solicitud, CancellationToken cancelacion = default);

    /// <summary>
    /// Lista los envíos recepcionados en depósito para su posterior clasificación por zona, prioridad y tipo (UC-15).
    /// </summary>
    Task<IReadOnlyList<EnvioEnDepositoDto>> ListarEnviosEnDepositoAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene los bultos recepcionados de un envío en depósito para clasificar operativamente.
    /// </summary>
    Task<EnvioEnDepositoDto?> ObtenerEnvioParaClasificacionAsync(Guid envioId, CancellationToken cancelacion = default);

    /// <summary>
    /// Registra la clasificación por zona, prioridad y tipo en cada bulto de un envío en depósito (UC-15).
    /// </summary>
    Task<ResultadoClasificacionDto> ClasificarBultosAsync(Guid envioId, ClasificarBultosRequestDto solicitud, CancellationToken cancelacion = default);

    /// <summary>
    /// Lista las hojas de ruta despachadas, en tránsito o finalizadas pendientes de rendición al regreso del repartidor (UC-16, RF-22).
    /// </summary>
    Task<IReadOnlyList<RutaPendienteRendicionDto>> ListarRutasPendientesRendicionAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene el detalle de una hoja de ruta para contrastar bultos y registrar la rendición (UC-16).
    /// </summary>
    Task<RutaParaRendicionDetalleDto?> ObtenerRutaParaRendicionAsync(Guid hojaDeRutaId, CancellationToken cancelacion = default);

    /// <summary>
    /// Registra la rendición de la hoja de ruta contrastando bultos devueltos, motivos internos y observaciones (UC-16, RF-22).
    /// </summary>
    Task<ResultadoRendicionRutaDto> RendirHojaDeRutaAsync(Guid hojaDeRutaId, RendirHojaDeRutaRequestDto solicitud, CancellationToken cancelacion = default);
}
