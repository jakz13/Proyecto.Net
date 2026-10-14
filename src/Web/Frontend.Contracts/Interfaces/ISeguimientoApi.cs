using UltimaMilla.Frontend.Contracts.Modelos.Seguimiento;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de servicio para la consulta pública y anónima de seguimiento de envíos (UC-33)
/// y solicitud de reprogramación de entrega (UC-34).
/// </summary>
public interface ISeguimientoApi
{
    /// <summary>
    /// Consulta el estado y la línea de tiempo de un envío mediante su código de seguimiento opaco.
    /// Si el código no existe, la implementación retornará null o un resultado genérico para evitar enumeración de envíos.
    /// </summary>
    Task<SeguimientoPublicoDto?> ConsultarSeguimientoAsync(string codigoSeguimiento, CancellationToken cancelacion = default);

    /// <summary>
    /// Registra una solicitud de reprogramación de entrega iniciada por el destinatario (UC-34).
    /// </summary>
    Task<bool> SolicitarReprogramacionAsync(string codigoSeguimiento, DateTime nuevaFecha, CancellationToken cancelacion = default);

    // TODO: Confirmar con backend si la reprogramación requiere validar franja horaria disponible provista por el endpoint.
}
