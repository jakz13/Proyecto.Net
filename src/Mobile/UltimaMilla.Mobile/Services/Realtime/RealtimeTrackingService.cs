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

        _hubConnection.On<string>("ReasignacionRecibida", async (mensaje) =>
        {
            _logger.LogInformation("SignalR: Evento ReasignacionRecibida recibido: {Mensaje}", mensaje);
            var detalle = string.IsNullOrWhiteSpace(mensaje)
                ? "reasignacion urgente desde central operativa"
                : mensaje.Trim();
            await EmitirAlertaRutaActualizadaAsync(Guid.Empty, detalle);
            await RefrescarRutasSilenciosamenteAsync();
        });

        _hubConnection.On<string, string>("NotificacionOperativa", async (titulo, mensaje) =>
        {
            _logger.LogInformation("SignalR: NotificacionOperativa recibida: {Titulo}", titulo);
            if (_notificacionService is not null)
            {
                await _notificacionService.MostrarNotificacionOperativaAsync(titulo, mensaje, "OPERACIONES");
            }
        });

        _hubConnection.On<JsonElement>("CambioEstadoRuta", async (datos) =>
        {
            var info = ExtraerInfoRuta(datos);
            _logger.LogInformation("SignalR: CambioEstadoRuta {HojaDeRutaId} -> {Estado}", info.HojaDeRutaId, info.Estado);

            if (EsEstadoAsignacionONuevaRuta(info.Estado))
            {
                await EmitirAlertaNuevaRutaAsync(info.HojaDeRutaId, info.CodigoRuta, info.TotalParadas);
            }
            else if (EsEstadoModificacionActiva(info.Estado))
            {
                await EmitirAlertaRutaActualizadaAsync(info.HojaDeRutaId, info.DetalleCambio);
            }

            await RefrescarRutasSilenciosamenteAsync();
        });

        _hubConnection.On<JsonElement>("RutaAsignada", async (datos) =>
        {
            var info = ExtraerInfoRuta(datos);
            _logger.LogInformation("SignalR: RutaAsignada {HojaDeRutaId}", info.HojaDeRutaId);
            await EmitirAlertaNuevaRutaAsync(info.HojaDeRutaId, info.CodigoRuta, info.TotalParadas);
            await RefrescarRutasSilenciosamenteAsync();
        });

        _hubConnection.On<JsonElement>("RutaLiberada", async (datos) =>
        {
            var info = ExtraerInfoRuta(datos);
            _logger.LogInformation("SignalR: RutaLiberada {HojaDeRutaId}", info.HojaDeRutaId);
            await EmitirAlertaNuevaRutaAsync(info.HojaDeRutaId, info.CodigoRuta, info.TotalParadas);
            await RefrescarRutasSilenciosamenteAsync();
        });

        _hubConnection.On<JsonElement>("RutaDespachada", async (datos) =>
        {
            var info = ExtraerInfoRuta(datos);
            _logger.LogInformation("SignalR: RutaDespachada {HojaDeRutaId}", info.HojaDeRutaId);
            await EmitirAlertaNuevaRutaAsync(info.HojaDeRutaId, info.CodigoRuta, info.TotalParadas);
            await RefrescarRutasSilenciosamenteAsync();
        });

        _hubConnection.On<JsonElement>("RutaActualizada", async (datos) =>
        {
            var info = ExtraerInfoRuta(datos);
            _logger.LogInformation("SignalR: RutaActualizada {HojaDeRutaId}", info.HojaDeRutaId);
            await EmitirAlertaRutaActualizadaAsync(info.HojaDeRutaId, info.DetalleCambio);
            await RefrescarRutasSilenciosamenteAsync();
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
        };

        _hubConnection.Closed += error =>
        {
            _logger.LogWarning(error, "SignalR: Conexion con el Hub de operaciones cerrada.");
            ConnectionStateChanged?.Invoke(this, false);
            return Task.CompletedTask;
        };
    }

    private async Task EmitirAlertaNuevaRutaAsync(Guid hojaDeRutaId, string codigoRuta, int totalParadas)
    {
        if (!DebeEmitirAlerta(hojaDeRutaId, "NUEVA_RUTA"))
        {
            return;
        }

        ObtenerSyncService()?.RegistrarNotificacionRutaEmitida(hojaDeRutaId, "NUEVA_RUTA");

        if (_notificacionService is null)
        {
            return;
        }

        var codigoVisible = string.IsNullOrWhiteSpace(codigoRuta) ? "del dia" : codigoRuta.Trim();
        var mensaje = $"Se habilito la ruta {codigoVisible} con {Math.Max(0, totalParadas)} paradas pendientes. Ingresa a la seccion Carga para verificar los bultos.";

        await _notificacionService.MostrarNotificacionOperativaAsync(
            "Nueva Hoja de Ruta Asignada",
            mensaje,
            "RUTA");
    }

    private async Task EmitirAlertaRutaActualizadaAsync(Guid hojaDeRutaId, string detalleCambio)
    {
        if (!DebeEmitirAlerta(hojaDeRutaId, "RUTA_ACTUALIZADA"))
        {
            return;
        }

        ObtenerSyncService()?.RegistrarNotificacionRutaEmitida(hojaDeRutaId, "RUTA_ACTUALIZADA");

        if (_notificacionService is null)
        {
            return;
        }

        var detalleVisible = string.IsNullOrWhiteSpace(detalleCambio)
            ? "modificacion de paradas desde central"
            : detalleCambio.Trim();

        await _notificacionService.MostrarNotificacionOperativaAsync(
            "Actualizacion en Hoja de Ruta Activa",
            $"Se actualizaron las paradas de tu ruta en curso ({detalleVisible}). Revisa el listado actualizado.",
            "RUTA");
    }

    private bool DebeEmitirAlerta(Guid hojaDeRutaId, string tipo)
    {
        var clave = $"{hojaDeRutaId}:{tipo}";
        var ahora = DateTimeOffset.UtcNow;

        if (_alertasRecientes.TryGetValue(clave, out var previo) && (ahora - previo) < TimeSpan.FromSeconds(15))
        {
            return false;
        }

        _alertasRecientes[clave] = ahora;
        return true;
    }

    private static bool EsEstadoAsignacionONuevaRuta(string? estado) =>
        estado is not null && (
            estado.Equals("Asignada", StringComparison.OrdinalIgnoreCase) ||
            estado.Equals("Liberada", StringComparison.OrdinalIgnoreCase) ||
            estado.Equals("EnCarga", StringComparison.OrdinalIgnoreCase) ||
            estado.Equals("Despachada", StringComparison.OrdinalIgnoreCase));

    private static bool EsEstadoModificacionActiva(string? estado) =>
        estado is not null && (
            estado.Equals("EnTransito", StringComparison.OrdinalIgnoreCase) ||
            estado.Equals("Reprogramada", StringComparison.OrdinalIgnoreCase) ||
            estado.Equals("Cancelada", StringComparison.OrdinalIgnoreCase) ||
            estado.Equals("Modificada", StringComparison.OrdinalIgnoreCase));

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

    private async Task RefrescarRutasSilenciosamenteAsync()
    {
        try
        {
            var syncService = ObtenerSyncService();
            if (syncService is not null)
            {
                await syncService.DescargarYActualizarRutasDelServidorAsync();
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
        CancellationToken cancellationToken = default,
        double? velocidadKmh = null,
        int? bateriaPct = null)
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
