using UltimaMilla.Frontend.Contracts.Modelos.Mensajeria;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de servicio para la gestión y reintento de mensajes fallidos en colas asíncronas (UC-42).
/// Permite al Administrador consultar la cola de fallidos (Dead Letter Queue) y solicitar el reintento manual
/// de uno o múltiples mensajes de la plataforma.
/// </summary>
public interface IMensajeriaApi
{
    /// <summary>
    /// Consulta los mensajes retenidos en la cola de fallidos aplicando filtros opcionales.
    /// </summary>
    Task<IReadOnlyList<MensajeFallidoDto>> ListarMensajesFallidosAsync(
        string? tipo = null,
        string? estado = null,
        string? busqueda = null,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene el detalle técnico y payload original de un mensaje fallido específico.
    /// </summary>
    Task<MensajeFallidoDto?> ObtenerMensajePorIdAsync(Guid id, CancellationToken cancelacion = default);

    /// <summary>
    /// Vuelve a encolar uno o múltiples mensajes para su reprocesamiento.
    /// Si 'forzarFallo' es verdadero, simula el flujo alternativo donde el mensaje vuelve a fallar y permanece en la cola con intentos incrementados.
    /// </summary>
    Task<ResultadoReintentoDto> ReintentarMensajesAsync(
        IEnumerable<Guid> ids,
        bool forzarFallo = false,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Descarta definitivamente un mensaje de la cola de fallidos si ya no requiere reprocesamiento.
    /// </summary>
    Task<bool> DescartarMensajeAsync(Guid id, CancellationToken cancelacion = default);

    /// <summary>
    /// Elimina definitivamente todos los mensajes de la lista que ya fueron resueltos con éxito.
    /// </summary>
    Task<int> LimpiarMensajesResueltosAsync(CancellationToken cancelacion = default);
}
