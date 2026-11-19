using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UltimaMilla.Mobile.Configuration;
using UltimaMilla.Mobile.Services.Auth;
using UltimaMilla.Mobile.Services.Handlers;
using UltimaMilla.Mobile.Services.Notifications;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.Services.Theme;

namespace UltimaMilla.Mobile.Services.Realtime;

/// <summary>
/// Cliente de SignalR resiliente y de alto rendimiento para transmision de telemetria y alertas en tiempo real.
/// Gestiona reconexiones automaticas, suscripcion al grupo del repartidor, inyeccion JWT y tolerancia a caidas de red movil.
/// </summary>
public class RealtimeTrackingService : IRealtimeTrackingService
{
    private readonly ILogger<RealtimeTrackingService> _logger;
    private readonly INotificacionLocalService? _notificacionService;
    private readonly IServiceProvider? _serviceProvider;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _alertasRecientes = new();
    private HubConnection? _hubConnection;
    private string? _currentHubUrl;
    private bool _isDisposed;

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler? RutaActualizadaEnTiempoReal;
    public event EventHandler<string>? DatosCambiaronEnTiempoReal;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    public HubConnectionState State => _hubConnection?.State ?? HubConnectionState.Disconnected;

    public RealtimeTrackingService(
        ILogger<RealtimeTrackingService> logger,
        INotificacionLocalService? notificacionService = null,
        IServiceProvider? serviceProvider = null)
    {
        _logger = logger;
        _notificacionService = notificacionService;
        _serviceProvider = serviceProvider;
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
                // Inyeccion del token Bearer persistido para autenticar el canal de SignalR
                options.AccessTokenProvider = async () =>
                {
                    try
                    {
                        var token = AuthDelegatingHandler.CurrentTokenInMemory
                                 ?? await SecureStorage.Default.GetAsync(AuthDelegatingHandler.AuthTokenKey);
                        if (!string.IsNullOrWhiteSpace(token))
                        {
                            return token;
                        }

                        return null;
                    }
                    catch
                    {
                        return null;
                    }
                };
            })
            // Estrategia de reconexion con retroceso incremental ante intermitencias en calle
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        // Ignorar explicitamente eventos de telemetria GPS y movimiento de vehiculos en el cliente movil
        _hubConnection.On<JsonElement>("PosicionRepartidorActualizada", _ => { });
        _hubConnection.On<JsonElement>("PosicionActualizada", _ => { });
        _hubConnection.On<JsonElement>("PosicionVehiculo", _ => { });

        _hubConnection.On<string>("SesionDesplazada", async (mensaje) =>
        {
            _logger.LogWarning("SignalR: Sesión desplazada notificada por el servidor: {Mensaje}", mensaje);
            await StopAsync();
            var auth = _serviceProvider?.GetService<IAuthService>();
            if (auth is not null)
            {
                await auth.ManejarSesionDesplazadaAsync(mensaje);
            }
        });

        _hubConnection.On<string>("ReasignacionRecibida", async (mensaje) =>
        {
            if (EsEventoTelemetriaGps( null, mensaje))
            {
                return;
            }

            _logger.LogInformation("SignalR: Evento ReasignacionRecibida recibido: {Mensaje}", mensaje);
            await RefrescarRutasSilenciosamenteAsync();
        });

        _hubConnection.On<string, string>("NotificacionOperativa", async (titulo, mensaje) =>
        {
            if (EsEventoTelemetriaGps(titulo, mensaje))
            {
                return;
            }

            _logger.LogInformation("SignalR: NotificacionOperativa recibida: {Titulo}", titulo);
            if (_notificacionService is not null)
            {
                await _notificacionService.MostrarNotificacionOperativaAsync(
                    titulo,
                    mensaje,
                    "OPERACIONES",
                    firmaContenido: $"{titulo}|{mensaje}");
            }
        });

        _hubConnection.On<JsonElement>("CambioEstadoRuta", async (datos) =>
        {
            var info = ExtraerInfoRuta(datos);
            if (EsEventoTelemetriaGps(info.Estado, info.DetalleCambio))
            {
                return;
            }

            _logger.LogInformation("SignalR: CambioEstadoRuta {HojaDeRutaId} -> {Estado}", info.HojaDeRutaId, info.Estado);
            await RefrescarRutasSilenciosamenteAsync();
        });

        _hubConnection.On<JsonElement>("RutaAsignada", async (datos) =>
        {
            var info = ExtraerInfoRuta(datos);
            _logger.LogInformation("SignalR: RutaAsignada {HojaDeRutaId}", info.HojaDeRutaId);
            await RefrescarRutasSilenciosamenteAsync();
        });

        _hubConnection.On<JsonElement>("RutaLiberada", async (datos) =>
        {
            var info = ExtraerInfoRuta(datos);
            _logger.LogInformation("SignalR: RutaLiberada {HojaDeRutaId}", info.HojaDeRutaId);
            await RefrescarRutasSilenciosamenteAsync();
        });

        _hubConnection.On<JsonElement>("RutaDespachada", async (datos) =>
        {
            var info = ExtraerInfoRuta(datos);
            _logger.LogInformation("SignalR: RutaDespachada {HojaDeRutaId}", info.HojaDeRutaId);
            await RefrescarRutasSilenciosamenteAsync();
        });

        _hubConnection.On<JsonElement>("RutaActualizada", async (datos) =>
        {
            var info = ExtraerInfoRuta(datos);
            if (EsEventoTelemetriaGps(info.Estado, info.DetalleCambio))
            {
                return;
            }

            _logger.LogInformation("SignalR: RutaActualizada {HojaDeRutaId}", info.HojaDeRutaId);
            await RefrescarRutasSilenciosamenteAsync();
        });

        // Aviso genérico de la API: algo cambió en el operador (una hoja rendida, un envío reasignado o cancelado, etc.). Se vuelve a pedir la ruta
        // sin esperar a la próxima acción del repartidor; varios avisos seguidos se juntan en una sola descarga.
        _hubConnection.On<JsonElement>("DatosCambiaron", datos =>
        {
            var area = LeerPropiedadString(datos, "area", "Area") ?? string.Empty;
            try
            {
                DatosCambiaronEnTiempoReal?.Invoke(this, area);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SignalR: una pantalla falló al reaccionar a DatosCambiaron.");
            }

            ProgramarRefrescoPorAvisoGenerico();
        });

        // La plataforma habilitó o deshabilitó una funcionalidad del operador: la descarga de rutas trae también las funcionalidades vigentes
        _hubConnection.On<JsonElement>("FuncionalidadesCambiaron", _ => ProgramarRefrescoPorAvisoGenerico());

        _hubConnection.On<JsonElement>("IdentidadVisualActualizada", async (_) =>
        {
            _logger.LogInformation("SignalR: IdentidadVisualActualizada recibida. Actualizando tema visual del operador...");
            var themeService = _serviceProvider?.GetService<IOperadorThemeService>();
            if (themeService is not null)
            {
                try
                {
                    await themeService.SincronizarIdentidadVisualAsync();
                }
                catch (Exception exTheme)
                {
                    _logger.LogWarning(exTheme, "SignalR: No se pudo actualizar la identidad visual desde SignalR.");
                }
            }
        });

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "SignalR: Conexion interrumpida en calle. Reconectando automaticamente a {HubUrl}...", hubUrl);
            ConnectionStateChanged?.Invoke(this, false);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += async connectionId =>
        {
            _logger.LogInformation("SignalR: Reconexion exitosa establecida con ID {ConnectionId}", connectionId);
            await RegistrarGrupoRepartidorAsync(CancellationToken.None);
            ConnectionStateChanged?.Invoke(this, true);

            var syncService = ObtenerSyncService();
            if (syncService is not null)
            {
                try
                {
                    if (await syncService.ObtenerCantidadPendientesAsync() > 0)
                    {
                        await syncService.SincronizarOperacionesPendientesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "SignalR: fallo no bloqueante al drenar cola pendiente tras reconexión.");
                }
            }

            await RefrescarRutasSilenciosamenteAsync();
        };

        _hubConnection.Closed += error =>
        {
            _logger.LogWarning(error, "SignalR: Conexion con el Hub de operaciones cerrada.");
            ConnectionStateChanged?.Invoke(this, false);
            return Task.CompletedTask;
        };
    }

    private static bool EsEventoTelemetriaGps(string? estadoOTitulo, string? detalleOMensaje)
    {
        if (ContieneIndicadorTelemetriaGps(estadoOTitulo) || ContieneIndicadorTelemetriaGps(detalleOMensaje))
        {
            return true;
        }

        return false;
    }

    private static bool ContieneIndicadorTelemetriaGps(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        return texto.Contains("Posicion", StringComparison.OrdinalIgnoreCase) ||
               texto.Contains("Posición", StringComparison.OrdinalIgnoreCase) ||
               texto.Contains("Coordenada", StringComparison.OrdinalIgnoreCase) ||
               texto.Contains("Latitud", StringComparison.OrdinalIgnoreCase) ||
               texto.Contains("Longitud", StringComparison.OrdinalIgnoreCase) ||
               texto.Contains("Telemetria", StringComparison.OrdinalIgnoreCase) ||
               texto.Contains("Telemetría", StringComparison.OrdinalIgnoreCase) ||
               texto.Contains("GPS", StringComparison.OrdinalIgnoreCase) ||
               texto.Contains("Bateria", StringComparison.OrdinalIgnoreCase) ||
               texto.Contains("Batería", StringComparison.OrdinalIgnoreCase) ||
               texto.Contains("Velocidad", StringComparison.OrdinalIgnoreCase);
    }

    private static (Guid HojaDeRutaId, string CodigoRuta, string Estado, int TotalParadas, string DetalleCambio) ExtraerInfoRuta(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return (Guid.Empty, string.Empty, string.Empty, 0, "actualizacion operativa");
        }

        Guid hojaDeRutaId = Guid.Empty;
        var hojaStr = LeerPropiedadString(payload, "hojaDeRutaId", "HojaDeRutaId");
        if (!string.IsNullOrWhiteSpace(hojaStr))
        {
            _ = Guid.TryParse(hojaStr, out hojaDeRutaId);
        }

        var codigoRuta = LeerPropiedadString(payload, "codigoRuta", "CodigoRuta", "numeroRuta", "NumeroRuta") ?? string.Empty;
        var estado = LeerPropiedadString(payload, "estado", "Estado") ?? string.Empty;
        var detalleCambio = LeerPropiedadString(payload, "detalleCambio", "DetalleCambio", "mensaje", "Mensaje")
            ?? "actualizacion de paradas";
        var totalParadas = LeerPropiedadInt(payload, "totalParadas", "TotalParadas");

        return (hojaDeRutaId, codigoRuta, estado, totalParadas, detalleCambio);
    }

    private static string? LeerPropiedadString(JsonElement obj, params string[] nombres)
    {
        foreach (var nombre in nombres)
        {
            if (obj.TryGetProperty(nombre, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        return null;
    }

    private static int LeerPropiedadInt(JsonElement obj, params string[] nombres)
    {
        foreach (var nombre in nombres)
        {
            if (obj.TryGetProperty(nombre, out var prop) && prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var valor))
            {
                return valor;
            }
        }
        return 0;
    }

    private ISyncService? ObtenerSyncService()
    {
        try
        {
            return _serviceProvider?.GetService<ISyncService>();
        }
        catch
        {
            return null;
        }
    }

    private static readonly TimeSpan EsperaAgrupacionAvisos = TimeSpan.FromSeconds(1.5);
    private int _refrescoGenericoProgramado;

    private void ProgramarRefrescoPorAvisoGenerico()
    {
        // Si ya hay una descarga esperando, este aviso ya queda cubierto por ella
        if (Interlocked.Exchange(ref _refrescoGenericoProgramado, 1) == 1)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(EsperaAgrupacionAvisos);
            }
            finally
            {
                Interlocked.Exchange(ref _refrescoGenericoProgramado, 0);
            }

            await RefrescarRutasSilenciosamenteAsync();
        });
    }

    private async Task RefrescarRutasSilenciosamenteAsync()
    {
        try
        {
            var syncService = ObtenerSyncService();
            if (syncService is not null && syncService.IsOnline)
            {
                await syncService.DescargarYActualizarRutasDelServidorAsync();
                RutaActualizadaEnTiempoReal?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SignalR: fallo no bloqueante al refrescar rutas tras evento del hub.");
        }
    }

    private async Task RegistrarGrupoRepartidorAsync(CancellationToken cancellationToken)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            return;
        }

        try
        {
            var auth = _serviceProvider?.GetService<IAuthService>();
            var repartidorId = auth?.RepartidorId;
            if (repartidorId.HasValue && repartidorId.Value != Guid.Empty)
            {
                await _hubConnection.SendAsync("RegistrarRepartidor", repartidorId.Value, cancellationToken);
                _logger.LogInformation("SignalR: Repartidor {RepartidorId} suscrito al grupo de OperacionHub.", repartidorId.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SignalR: No se pudo invocar RegistrarRepartidor (puede estar ya suscrito por JWT).");
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (ObtenerSyncService()?.IsOfflineSimulado == true)
            return;

        var tokenDisponible = _serviceProvider?.GetService<IAuthService>()?.AccessToken
            ?? AuthDelegatingHandler.CurrentTokenInMemory;

        if (e.NetworkAccess == NetworkAccess.Internet
            && (_hubConnection == null || _hubConnection.State == HubConnectionState.Disconnected)
            && !string.IsNullOrWhiteSpace(tokenDisponible))
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
        if (_isDisposed || ObtenerSyncService()?.IsOfflineSimulado == true)
            return;

        bool sesionNoAutorizada = false;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            EnsureHubConnection();

            if (_hubConnection!.State == HubConnectionState.Connected)
            {
                await RegistrarGrupoRepartidorAsync(cancellationToken);
                return;
            }

            if (_hubConnection.State == HubConnectionState.Connecting ||
                _hubConnection.State == HubConnectionState.Reconnecting)
            {
                return;
            }

            var hubUrl = $"{ApiEndpoints.BaseUrl.TrimEnd('/')}{ApiEndpoints.OperacionHub}";
            _logger.LogInformation("SignalR: Conectando con Hub de operaciones en {HubUrl}...", hubUrl);

            await _hubConnection.StartAsync(cancellationToken);
            await RegistrarGrupoRepartidorAsync(cancellationToken);
            _logger.LogInformation("SignalR: Conexión establecida con éxito.");
            ConnectionStateChanged?.Invoke(this, true);
        }
        catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            sesionNoAutorizada = true;
            _logger.LogWarning(httpEx, "SignalR: Negociación rechazada con HTTP 401 por sesión desplazada o revocada.");
            ConnectionStateChanged?.Invoke(this, false);
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

        if (sesionNoAutorizada)
        {
            var auth = _serviceProvider?.GetService<IAuthService>();
            if (auth is not null)
            {
                await auth.ManejarSesionDesplazadaAsync();
            }
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
        CancellationToken cancellationToken = default,
        double? velocidadKmh = null,
        int? bateriaPct = null)
    {
        if (_isDisposed || ObtenerSyncService()?.IsOfflineSimulado == true)
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
                    DateTime.UtcNow,
                    velocidadKmh,
                    bateriaPct
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
