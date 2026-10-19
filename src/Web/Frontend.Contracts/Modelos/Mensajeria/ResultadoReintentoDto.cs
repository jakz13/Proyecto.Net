namespace UltimaMilla.Frontend.Contracts.Modelos.Mensajeria;

/// <summary>
/// Resultado del reprocesamiento manual de uno o varios mensajes de la cola de fallidos (UC-42).
/// </summary>
public record ResultadoReintentoDto
{
    /// <summary>
    /// Total de mensajes seleccionados para reintento.
    /// </summary>
    public int TotalSeleccionados { get; init; }

    /// <summary>
    /// Cantidad de mensajes que pudieron reprocesarse y resolverse satisfactoriamente.
    /// </summary>
    public int Exitosos { get; init; }

    /// <summary>
    /// Cantidad de mensajes que fallaron nuevamente y permanecen en la cola con intentos incrementados (flujo alternativo).
    /// </summary>
    public int Fallidos { get; init; }

    /// <summary>
    /// Mensaje descriptivo con el balance de la operación para mostrar en la interfaz.
    /// </summary>
    public string MensajeResumen { get; init; } = string.Empty;

    /// <summary>
    /// Detalle individual por cada mensaje procesado.
    /// </summary>
    public IReadOnlyList<string> Detalles { get; init; } = Array.Empty<string>();
}
