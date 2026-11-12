using UltimaMilla.Frontend.Contracts.Modelos.Seguimiento;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de servicio para la consulta pública y anónima de seguimiento de envíos
/// y solicitud de reprogramación de entrega.
/// </summary>
public interface ISeguimientoApi
{
    /// <summary>
    /// Consulta el estado y la línea de tiempo de un envío mediante su código de seguimiento opaco.
    /// Si el código no existe, la implementación retornará null o un resultado genérico para evitar enumeración de envíos.
    /// </summary>
    Task<SeguimientoPublicoDto?> ConsultarSeguimientoAsync(string codigoSeguimiento, CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene las franjas horarias alternativas disponibles para la zona del envío.
    /// </summary>
    Task<FranjasDisponiblesDto?> ObtenerFranjasDisponiblesAsync(string codigoSeguimiento, CancellationToken cancelacion = default);

    /// <summary>
    /// Envía la solicitud de cambio de franja horaria a evaluación del operador.
    /// </summary>
    Task<ResultadoReprogramacionDto> SolicitarReprogramacionAsync(string codigoSeguimiento, Guid franjaHorariaId, CancellationToken cancelacion = default);

    /// <summary>
    /// Registra una solicitud de reprogramación de entrega iniciada por el destinatario (compatibilidad previa).
    /// </summary>
    Task<bool> SolicitarReprogramacionAsync(string codigoSeguimiento, DateTime nuevaFecha, CancellationToken cancelacion = default);
}
