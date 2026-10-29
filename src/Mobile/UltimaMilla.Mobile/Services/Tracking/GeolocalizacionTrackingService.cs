using Microsoft.Extensions.Logging;
using UltimaMilla.Mobile.Services.Realtime;

namespace UltimaMilla.Mobile.Services.Tracking;

/// <summary>
/// Servicio de rastreo de geolocalización periódico y optimizado para baterías móviles (Hito 4 / RNF 6.11).
/// Utiliza PeriodicTimer de .NET 10, captura de GPS con precisión balanceada y fallback resiliente.
/// </summary>
public class GeolocalizacionTrackingService : IGeolocalizacionTrackingService
{
    private readonly IRealtimeTrackingService _realtimeTrackingService;
    private readonly ILogger<GeolocalizacionTrackingService> _logger;
    private readonly SemaphoreSlim _trackingLock = new(1, 1);

    private CancellationTokenSource? _trackingCts;
    private Task? _trackingTask;
    private bool _isDisposed;

    public bool IsTrackingActive { get; private set; }
    public Location? UltimaUbicacionConocida { get; private set; }

    // RNF 6.11: Captura cada 20 segundos por defecto (entre 15 y 30s) para maximizar la autonomía de batería
    public TimeSpan IntervaloTracking { get; set; } = TimeSpan.FromSeconds(20);

    public event EventHandler<PosicionActualizadaEventArgs>? OnPosicionActualizada;
    public event EventHandler<bool>? OnTrackingStateChanged;

    public GeolocalizacionTrackingService(
        IRealtimeTrackingService realtimeTrackingService,
        ILogger<GeolocalizacionTrackingService> logger)
    {
        _realtimeTrackingService = realtimeTrackingService;
        _logger = logger;
    }

    public async Task<bool> SolicitarPermisosUbicacionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status == PermissionStatus.Granted)
            {
                // En Android e iOS intentamos verificar o solicitar permisos de fondo si la plataforma lo admite
                try
                {
                    var alwaysStatus = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
                    if (alwaysStatus != PermissionStatus.Granted)
                    {
                        await Permissions.RequestAsync<Permissions.LocationAlways>();
                    }
                }
                catch
                {
                    // No bloqueante si el sistema operativo no admite escalado a LocationAlways
                }

                return true;
            }

            _logger.LogWarning("Geolocalización: El usuario denegó los permisos de ubicación ({Status}).", status);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Geolocalización: Excepción al gestionar permisos de ubicación.");
            return false;
        }
    }

    public async Task<Location?> ObtenerUbicacionActualAsync(CancellationToken cancellationToken = default)
    {
        Location? location = null;

        try
        {
            // 1. Intentar obtener posición GPS con precisión Media (optimización de consumo de energía)
            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8));
            location = await Geolocation.Default.GetLocationAsync(request, cancellationToken);

            if (location != null)
            {
                UltimaUbicacionConocida = location;
                return location;
            }
        }
        catch (FeatureNotSupportedException)
        {
            _logger.LogInformation("Geolocalización: El hardware o la plataforma no soporta GPS nativo.");
        }
        catch (PermissionException)
        {
            _logger.LogWarning("Geolocalización: Permisos de ubicación no concedidos.");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Geolocalización: No se pudo obtener posición instantánea precisa.");
        }

        try
        {
            // 2. Fallback rápido: última ubicación conocida en caché del sistema operativo
            var fallback = await Geolocation.Default.GetLastKnownLocationAsync();
            if (fallback != null)
            {
                UltimaUbicacionConocida = fallback;
                return fallback;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Geolocalización: Error al consultar última ubicación conocida.");
        }

        // 3. Fallback simulado para entorno de pruebas / Emulador / Windows Desktop
        if (location == null && (DeviceInfo.DeviceType == DeviceType.Virtual || DeviceInfo.Platform == DevicePlatform.WinUI))
        {
            // Coordenadas céntricas de Montevideo para demos y pruebas de laboratorio
            location = new Location(-34.901118, -56.164531)
            {
                Accuracy = 15.0,
                Timestamp = DateTimeOffset.UtcNow
            };
            UltimaUbicacionConocida = location;
            return location;
        }

        return UltimaUbicacionConocida;
    }

    public async Task IniciarTrackingAsync(
        Guid hojaDeRutaId,
        Guid repartidorId,
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            return;

        await _trackingLock.WaitAsync(cancellationToken);
        try
        {
            if (IsTrackingActive)
            {
                _logger.LogInformation("Geolocalización: El rastreo ya se encuentra en ejecución.");
                return;
            }

            var tienePermiso = await SolicitarPermisosUbicacionAsync();
            if (!tienePermiso)
            {
                _logger.LogWarning("Geolocalización: No se puede iniciar el rastreo sin permisos de ubicación.");
                return;
            }

            // Iniciar conexión de SignalR en segundo plano si hay red disponible
            await _realtimeTrackingService.StartAsync(cancellationToken);

            _trackingCts = new CancellationTokenSource();
            IsTrackingActive = true;
            OnTrackingStateChanged?.Invoke(this, true);

            _trackingTask = Task.Run(() => EjecutarBucleTrackingAsync(hojaDeRutaId, repartidorId, _trackingCts.Token));
            _logger.LogInformation("Geolocalización: Rastreo en tiempo real activado para Ruta {RutaId}, Repartidor {RepartidorId}.", hojaDeRutaId, repartidorId);
        }
        finally
        {
            _trackingLock.Release();
        }
    }

    public async Task DetenerTrackingAsync()
    {
        await _trackingLock.WaitAsync();
        try
        {
            if (!IsTrackingActive)
                return;

            _logger.LogInformation("Geolocalización: Deteniendo rastreo en tiempo real...");
            IsTrackingActive = false;
            OnTrackingStateChanged?.Invoke(this, false);

            if (_trackingCts != null)
            {
                _trackingCts.Cancel();
                _trackingCts.Dispose();
                _trackingCts = null;
            }

            if (_trackingTask != null)
            {
                try
                {
                    await _trackingTask;
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Geolocalización: Excepción al esperar finalización de la tarea de rastreo.");
                }

                _trackingTask = null;
            }

            // Detener el canal de SignalR al finalizar la ruta para no consumir recursos innecesarios
            await _realtimeTrackingService.StopAsync();
            _logger.LogInformation("Geolocalización: Rastreo detenido y recursos liberados.");
        }
        finally
        {
            _trackingLock.Release();
        }
    }

    private async Task EjecutarBucleTrackingAsync(Guid hojaDeRutaId, Guid repartidorId, CancellationToken ct)
    {
        // PeriodicTimer en .NET 10: máxima eficiencia y sin sobrecarga del hilo de UI
        using var timer = new PeriodicTimer(IntervaloTracking);

        // Disparo inicial inmediato al salir a la calle
        await CapturarYTransmitirAsync(hojaDeRutaId, repartidorId, ct);

        try
        {
            while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
            {
                await CapturarYTransmitirAsync(hojaDeRutaId, repartidorId, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelación limpia controlada
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Geolocalización: Error crítico en bucle de telemetría.");
        }
    }

    private async Task CapturarYTransmitirAsync(Guid hojaDeRutaId, Guid repartidorId, CancellationToken ct)
    {
        try
        {
            var location = await ObtenerUbicacionActualAsync(ct);
            if (location != null)
            {
                bool transmitida = _realtimeTrackingService.IsConnected;

                OnPosicionActualizada?.Invoke(this, new PosicionActualizadaEventArgs
                {
                    Latitud = location.Latitude,
                    Longitud = location.Longitude,
                    PrecisionMetros = location.Accuracy,
                    Timestamp = location.Timestamp.UtcDateTime,
                    TransmitidaSignalR = transmitida
                });

                await _realtimeTrackingService.EnviarUbicacionAsync(
                    hojaDeRutaId,
                    repartidorId,
                    location.Latitude,
                    location.Longitude,
                    ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geolocalización: Error al muestrear y transmitir ubicación en el ciclo actual.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        await DetenerTrackingAsync();
        _trackingLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
