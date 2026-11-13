using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using UltimaMilla.Mobile.Configuration;
using UltimaMilla.Mobile.Services.Handlers;
using UltimaMilla.Mobile.Services.Notifications;

namespace UltimaMilla.Mobile.Services.Realtime;

/// <summary>
/// Cliente de SignalR resiliente y de alto rendimiento para transmisión de telemetría en tiempo real .
/// Gestiona reconexiones automáticas, inyección de credenciales JWT y tolerancia a caídas de red móvil.
/// </summary>
public class RealtimeTrackingService : IRealtimeTrackingService
{
    private readonly ILogger<RealtimeTrackingService> _logger;
    private readonly INotificacionLocalService? _notificacionService;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private HubConnection? _hubConnection;
    private string? _currentHubUrl;
    private bool _isDisposed;

    public event EventHandler<bool>? ConnectionStateChanged;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    public HubConnectionState State => _hubConnection?.State ?? HubConnectionState.Disconnected;

    public RealtimeTrackingService(ILogger<RealtimeTrackingService> logger, INotificacionLocalService? notificacionService = null)
    {
        _logger = logger;
        _notificacionService = notificacionService;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    private void EnsureHubConnection()
    {
        var hubUrl = $"{ApiEndpoints.BaseUrl.TrimEnd('/')}{ApiEndpoints.OperacionHub}";

        if (_hubConnection != null && string.Equals(_currentHubUrl, hubUrl, StringComparison.OrdinalIgnoreCase))
            return;

        if (_hubConnection != null)
        {
            try
            {
                _ = _hubConnection.DisposeAsync();
            }
            catch { }
            _hubConnection = null;
        }

        _currentHubUrl = hubUrl;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                // Inyección del token Bearer persistido para autenticar el canal de SignalR
                options.AccessTokenProvider = async () =>
                {
                    try
                    {
                        var token = await SecureStorage.Default.GetAsync(AuthDelegatingHandler.AuthTokenKey);
                        if (!string.IsNullOrWhiteSpace(token))
                        {
                            return token;
                        }

                        // En servidor local en desarrollo se tolera 'dev-token', pero en la nube debe ser null
                        return ApiEndpoints.EsServidorNube ? null : "dev-token";
                    }
                    catch
                    {
                        return null;
                    }
                };
            })
            // Estrategia de reconexión con retroceso incremental ante intermitencias en calle
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        _hubConnection.On<string>("ReasignacionRecibida", (mensaje) =>
        {
            _logger.LogInformation("SignalR: Evento de reasignacion urgente recibido: {Mensaje}", mensaje);
            _notificacionService?.MostrarAlertaOperativa("Reasignacion Urgente", mensaje);
            if (Shell.Current != null)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        if (Shell.Current != null)
                            await Shell.Current.DisplayAlertAsync("Reasignacion Urgente", mensaje, "Aceptar");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "SignalR: No se pudo mostrar alerta en pantalla.");
                    }
                });
            }
        });

        _hubConnection.On<string, string>("NotificacionOperativa", (titulo, mensaje) =>
        {
            _logger.LogInformation("SignalR: Notificacion operativa recibida: {Titulo} - {Mensaje}", titulo, mensaje);
            _notificacionService?.MostrarAlertaOperativa(titulo, mensaje);
            if (Shell.Current != null)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        if (Shell.Current != null)
                            await Shell.Current.DisplayAlertAsync(titulo, mensaje, "Aceptar");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "SignalR: No se pudo mostrar notificación operativa en pantalla.");
                    }
                });
            }
        });

        _hubConnection.On<object>("RutaActualizada", (datos) =>
        {
            _logger.LogInformation("SignalR: Evento de actualizacion de ruta recibido.");
            _notificacionService?.MostrarAlertaOperativa("Hoja de Ruta Actualizada", "Se registraron modificaciones en las paradas asignadas.");
        });

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "SignalR: Conexión interrumpida en calle. Reconectando automáticamente a {HubUrl}...", hubUrl);
            ConnectionStateChanged?.Invoke(this, false);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR: Reconexión exitosa establecida con ID {ConnectionId}", connectionId);
            ConnectionStateChanged?.Invoke(this, true);
            return Task.CompletedTask;
        };

        _hubConnection.Closed += error =>
        {
            _logger.LogWarning(error, "SignalR: Conexión con el Hub de operaciones cerrada.");
            ConnectionStateChanged?.Invoke(this, false);
            return Task.CompletedTask;
        };
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet && _hubConnection?.State == HubConnectionState.Disconnected)
        {
            _logger.LogInformation("SignalR: Acceso a internet restablecido. Reanudando conexión...");
            _ = Task.Run(async () =>
            {
                try
                {
                    await StartAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Fallo al intentar reconectar tras cambio de conectividad.");
                }
            });
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            return;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            EnsureHubConnection();

            if (_hubConnection!.State == HubConnectionState.Connected ||
                _hubConnection.State == HubConnectionState.Connecting ||
                _hubConnection.State == HubConnectionState.Reconnecting)
            {
                return;
            }

            var hubUrl = $"{ApiEndpoints.BaseUrl.TrimEnd('/')}{ApiEndpoints.OperacionHub}";
            _logger.LogInformation("SignalR: Conectando con Hub de operaciones en {HubUrl}...", hubUrl);

            await _hubConnection.StartAsync(cancellationToken);
            _logger.LogInformation("SignalR: Conexión establecida con éxito.");
            ConnectionStateChanged?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR: No se pudo conectar al Hub (la aplicación operará en modo offline/degradado).");
            ConnectionStateChanged?.Invoke(this, false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection == null)
            return;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_hubConnection.State != HubConnectionState.Disconnected)
            {
                _logger.LogInformation("SignalR: Cerrando conexión de forma limpia.");
                await _hubConnection.StopAsync(cancellationToken);
                ConnectionStateChanged?.Invoke(this, false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR: Error al detener conexión.");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task EnviarUbicacionAsync(
        Guid hojaDeRutaId,
        Guid repartidorId,
        double latitud,
        double longitud,
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            return;

        try
        {
            // Si la conexión cayó y hay internet disponible, intentamos reconectar rápidamente
            if (_hubConnection == null || _hubConnection.State == HubConnectionState.Disconnected)
            {
                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    await StartAsync(cancellationToken);
                }
            }

            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                var payload = new UbicacionTransmisionDto(
                    hojaDeRutaId,
                    repartidorId,
                    latitud,
                    longitud,
                    DateTime.UtcNow
                );

                // Invocar método en el Hub con send asíncrono no bloqueante
                await _hubConnection.SendAsync("TransmitirUbicacion", payload, cancellationToken);
                _logger.LogDebug("SignalR: Coordenadas transmitidas (Lat: {Lat:F5}, Lng: {Lng:F5}, Ruta: {RutaId})", latitud, longitud, hojaDeRutaId);
            }
            else
            {
                _logger.LogDebug("SignalR: Estado actual {State}. Ubicación omitida para streaming en vivo.", _hubConnection?.State);
            }
        }
        catch (Exception ex)
        {
            // Enfoque offline-first: los fallos de streaming no interrumpen la experiencia del repartidor
            _logger.LogWarning(ex, "SignalR: Error al transmitir ubicación GPS hacia el backend.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;

        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.DisposeAsync();
            }
            catch
            {
                // Ignorar excepciones en proceso de cierre
            }
        }

        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
