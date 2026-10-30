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
    public string PerfilMuestreoActivo { get; init; } = string.Empty;
    public int IntervaloSegundos { get; init; }
    public double? DistanciaProximaParadaMetros { get; init; }
}

/// <summary>
/// Telemetría consolidada del muestreo adaptativo por geocerca e impacto en batería (Sección 7.3).
/// </summary>
public record TelemetriaBateriaGeocercaDto(
    int PorcentajeBateria,
    string EstadoBateria,
    bool AhorroEnergiaActivo,
    string PerfilAdaptativoActual,
    int IntervaloSegundosActual,
    string PrecisionGpsActual,
    double ConsumoEstimadoMahHora,
    double AhorroEstimadoPorcentaje,
    int LecturasGpsRealizadas,
    double? DistanciaProximaParadaMetros,
    string? CodigoProximaParada
);

/// <summary>
/// Contrato del servicio de geolocalización en segundo plano, muestreo adaptativo y geocercas (Hito 4 / Sección 7.3).
/// </summary>
public interface IGeolocalizacionTrackingService : IAsyncDisposable
{
    bool IsTrackingActive { get; }

    Location? UltimaUbicacionConocida { get; }

    TimeSpan IntervaloTracking { get; set; }

    int LecturasGpsRealizadas { get; }

    string PerfilAdaptativoActual { get; }

    event EventHandler<PosicionActualizadaEventArgs>? OnPosicionActualizada;

    event EventHandler<bool>? OnTrackingStateChanged;

    Task<bool> SolicitarPermisosUbicacionAsync();

    Task<Location?> ObtenerUbicacionActualAsync(CancellationToken cancellationToken = default);

    Task IniciarTrackingAsync(Guid hojaDeRutaId, Guid repartidorId, CancellationToken cancellationToken = default);

    Task DetenerTrackingAsync();

    Task<EvaluacionGeocercasResult> SimularPosicionGpsAsync(
        double latitud,
        double longitud,
        DateTime? timestampUtc = null,
        CancellationToken cancellationToken = default);

    TelemetriaBateriaGeocercaDto ObtenerTelemetriaBateriaActual();

    Task<string> ObtenerInformeBateriaEHistorialGeocercasAsync();
}
