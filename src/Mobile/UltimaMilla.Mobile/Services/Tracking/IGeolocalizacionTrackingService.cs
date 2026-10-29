namespace UltimaMilla.Mobile.Services.Tracking;

/// <summary>
/// Argumentos de telemetría emitidos ante cada actualización de posición GPS.
/// </summary>
public class PosicionActualizadaEventArgs : EventArgs
{
    public double Latitud { get; init; }
    public double Longitud { get; init; }
    public double? PrecisionMetros { get; init; }
    public DateTime Timestamp { get; init; }
    public bool TransmitidaSignalR { get; init; }
}

/// <summary>
/// Contrato del servicio de geolocalización en segundo plano y captura periódica (Hito 4).
/// </summary>
public interface IGeolocalizacionTrackingService : IAsyncDisposable
{
    /// <summary>
    /// Indica si el rastreo periódico se encuentra en ejecución.
    /// </summary>
    bool IsTrackingActive { get; }

    /// <summary>
    /// Última ubicación capturada y validada por el servicio.
    /// </summary>
    Location? UltimaUbicacionConocida { get; }

    /// <summary>
    /// Intervalo de muestreo periódico para preservar la batería (por defecto 20 segundos).
    /// </summary>
    TimeSpan IntervaloTracking { get; set; }

    /// <summary>
    /// Evento disparado cada vez que se obtiene una nueva posición geográfica.
    /// </summary>
    event EventHandler<PosicionActualizadaEventArgs>? OnPosicionActualizada;

    /// <summary>
    /// Evento disparado cuando el rastreo se activa o desactiva.
    /// </summary>
    event EventHandler<bool>? OnTrackingStateChanged;

    /// <summary>
    /// Verifica y solicita de forma interactiva los permisos de ubicación al usuario.
    /// </summary>
    Task<bool> SolicitarPermisosUbicacionAsync();

    /// <summary>
    /// Obtiene la ubicación instantánea con fallback a la última posición conocida.
    /// </summary>
    Task<Location?> ObtenerUbicacionActualAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inicia el bucle de rastreo periódico para la hoja de ruta y repartidor en curso.
    /// </summary>
    Task IniciarTrackingAsync(Guid hojaDeRutaId, Guid repartidorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detiene el bucle periódico y libera los recursos del GPS y SignalR.
    /// </summary>
    Task DetenerTrackingAsync();
}
