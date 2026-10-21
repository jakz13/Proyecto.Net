using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.DTOs;
using UltimaMilla.Mobile.Models.Local;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.ViewModels.Ruta;

namespace UltimaMilla.Mobile.Services.Sync;

public class SyncStateChangedEventArgs : EventArgs
{
    public bool IsOnline { get; init; }
    public bool IsOfflineSimulado { get; init; }
    public int OperacionesPendientes { get; init; }
    public bool IsSyncing { get; init; }
    public string? Mensaje { get; init; }
}

public record SyncResult(
    int TotalProcesadas,
    int Exitosas,
    int ErroresNegocio,
    bool DetenidoPorCorteDeRed,
    string Mensaje,
    List<string>? DetallesErrores = null
);

public interface ISyncService
{
    bool IsOfflineSimulado { get; set; }
    bool IsOnline { get; }
    bool IsSyncing { get; }

    event EventHandler<SyncStateChangedEventArgs>? OnSyncStateChanged;

    Task<List<ParadaItemViewModel>> DescargarRutaDelDiaAsync(Guid? hojaDeRutaId = null, CancellationToken cancellationToken = default);
    Task<SyncResult> SincronizarOperacionesPendientesAsync(CancellationToken cancellationToken = default);
    Task AlternarModoOfflineSimuladoAsync();
    Task<int> ObtenerCantidadPendientesAsync();
    Task<string> ReiniciarDemoCompletaAsync(CancellationToken cancellationToken = default);
}

public class SyncService : ISyncService, IDisposable
{
    private readonly ILocalDatabaseService _localDatabaseService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SyncService> _logger;
    private readonly SemaphoreSlim _syncSemaphore = new(1, 1);

    private bool _isOfflineSimulado = false;
    private bool _isSyncing = false;
    private bool _disposed;

    public event EventHandler<SyncStateChangedEventArgs>? OnSyncStateChanged;

    public bool IsOfflineSimulado
    {
        get => _isOfflineSimulado;
        set
        {
            if (_isOfflineSimulado != value)
            {
                _isOfflineSimulado = value;
                _logger.LogInformation("Modo offline simulado cambiado a: {Value}", value);
                _ = OnModoConectividadCambiadoAsync();
            }
        }
    }

    public bool IsOnline => !IsOfflineSimulado && Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public bool IsSyncing
    {
        get => _isSyncing;
        private set => _isSyncing = value;
    }

    public SyncService(
        ILocalDatabaseService localDatabaseService,
        IHttpClientFactory httpClientFactory,
        ILogger<SyncService> logger)
    {
        _localDatabaseService = localDatabaseService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        _logger.LogInformation("Evento de conectividad de red detectado: {NetworkAccess}", e.NetworkAccess);
        _ = OnModoConectividadCambiadoAsync();
    }

    private async Task OnModoConectividadCambiadoAsync()
    {
        await NotificarCambioEstadoAsync();

        // Disparar sincronización automática en segundo plano cuando la señal vuelve
        if (IsOnline)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("Conexión restablecida. Iniciando sincronización automática de operaciones pendientes...");
                    await SincronizarOperacionesPendientesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error durante la sincronización automática reactiva de red.");
                }
            });
        }
    }

    public async Task AlternarModoOfflineSimuladoAsync()
    {
        IsOfflineSimulado = !IsOfflineSimulado;
        await Task.CompletedTask;
    }

    public async Task<int> ObtenerCantidadPendientesAsync()
    {
        return await _localDatabaseService.ObtenerCantidadPendientesAsync();
    }

    /// <summary>
    /// UC-17: Descarga la hoja de ruta desde el backend hacia SQLite si hay conexión.
    /// Si no hay red o está en modo offline simulado, hace fallback transparente leyendo de SQLite.
    /// </summary>
    public async Task<List<ParadaItemViewModel>> DescargarRutaDelDiaAsync(Guid? hojaDeRutaId = null, CancellationToken cancellationToken = default)
    {
        var targetRutaId = hojaDeRutaId ?? DemoDataDefaults.DefaultHojaDeRutaId;

        // Si estamos Online, intentamos consultar el endpoint oficial
        if (IsOnline)
        {
            try
            {
                _logger.LogInformation("Descargando hoja de ruta {RutaId} desde backend...", targetRutaId);
                var client = _httpClientFactory.CreateClient("BackendApi");
                var url = $"/api/mobile/v1/rutas/{targetRutaId}/paradas";

                var paradasDto = await client.GetFromJsonAsync<List<ParadaNavegacionDto>>(url, cancellationToken);
                if (paradasDto != null && paradasDto.Count > 0)
                {
                    await _localDatabaseService.GuardarRutaDescargadaAsync(targetRutaId, paradasDto);
                    _logger.LogInformation("Hoja de ruta {RutaId} descargada y persistida en SQLite ({Count} paradas).", targetRutaId, paradasDto.Count);

                    var localActualizado = await _localDatabaseService.ObtenerRutaActivaLocalAsync();
                    if (localActualizado != null && localActualizado.Paradas.Count > 0)
                    {
                        await NotificarCambioEstadoAsync("Ruta actualizada desde backend.");
                        return localActualizado.Paradas;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No fue posible descargar la ruta desde backend en {RutaId}. Continuando en modo offline local.", targetRutaId);
            }
        }

        // Fallback transparente: Recuperar desde SQLite local cifrado
        var local = await _localDatabaseService.ObtenerRutaActivaLocalAsync();
        if (local != null && local.Paradas.Count > 0)
        {
            _logger.LogInformation("Cargando {Count} paradas desde base local SQLite cifrada.", local.Paradas.Count);
            await NotificarCambioEstadoAsync("Ruta cargada desde SQLite local.");
            return local.Paradas;
        }

        // Si la base local estuviera completamente vacía (primera instalación sin red):
        await _localDatabaseService.SeedRutaDemoSiEstaVaciaAsync();
        var seedData = await _localDatabaseService.ObtenerRutaActivaLocalAsync();
        await NotificarCambioEstadoAsync("Ruta demo inicializada localmente.");
        return seedData?.Paradas ?? new List<ParadaItemViewModel>();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null
    };

    /// <summary>
    /// Vaciado FIFO de la cola de operaciones offline hacia los endpoints de MobileRutasEndpoints.
    /// Resiliente a cortes de red y errores de negocio.
    /// </summary>
    public async Task<SyncResult> SincronizarOperacionesPendientesAsync(CancellationToken cancellationToken = default)
    {
        if (!await _syncSemaphore.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
        {
            _logger.LogWarning("Sincronización omitida: Ya existe una ejecución en proceso.");
            return new SyncResult(0, 0, 0, false, "Ya existe una sincronización en progreso.");
        }

        IsSyncing = true;
        await NotificarCambioEstadoAsync("Sincronizando operaciones con la central...");

        int procesadas = 0;
        int exitosas = 0;
        int erroresNegocio = 0;
        bool detenidoPorCorteDeRed = false;
        var detallesErrores = new List<string>();

        try
        {
            if (!IsOnline)
            {
                return new SyncResult(0, 0, 0, false, "Dispositivo en Modo Offline (simulado o sin señal de red).", detallesErrores);
            }

            var pendientes = await _localDatabaseService.ObtenerOperacionesPendientesAsync();
            if (pendientes.Count == 0)
            {
                return new SyncResult(0, 0, 0, false, "No hay operaciones pendientes de sincronización.", detallesErrores);
            }

            _logger.LogInformation("Iniciando envío FIFO de {Count} operaciones pendientes...", pendientes.Count);
            var client = _httpClientFactory.CreateClient("BackendApi");

            foreach (var op in pendientes)
            {
                if (!IsOnline)
                {
                    detenidoPorCorteDeRed = true;
                    _logger.LogWarning("Pérdida de conectividad detectada durante el vaciado de cola. Deteniendo en operación {Id}.", op.Id);
                    break;
                }

                procesadas++;
                try
                {
                    switch (op.TipoOperacion)
                    {
                        case TiposOperacionOffline.ConfirmarCarga:
                        {
                            var payload = JsonSerializer.Deserialize<ConfirmarCargaOfflinePayload>(op.PayloadJson, JsonOptions);
                            if (payload != null)
                            {
                                var req = new
                                {
                                    CodigosBultosCargados = payload.CodigosBultosCargados,
                                    IdempotencyKey = payload.IdempotencyKey,
                                    ObservacionesDiscrepancia = payload.ObservacionesDiscrepancia
                                };

                                var url = $"/api/mobile/v1/rutas/{payload.HojaDeRutaId}/carga/confirmar";
                                var response = await client.PostAsJsonAsync(url, req, cancellationToken);
                                bool ok = await ProcesarRespuestaHttpAsync(op, response, "ConfirmarCarga", detallesErrores, cancellationToken);
                                if (ok) exitosas++; else erroresNegocio++;
                            }
                            break;
                        }

                        case TiposOperacionOffline.SeleccionarParada:
                        {
                            var payload = JsonSerializer.Deserialize<SeleccionarParadaOfflinePayload>(op.PayloadJson, JsonOptions);
                            if (payload != null)
                            {
                                var req = new
                                {
                                    LatitudActual = payload.LatitudActual,
                                    LongitudActual = payload.LongitudActual,
                                    Timestamp = payload.Timestamp,
                                    Accion = payload.Accion
                                };

                                var url = $"/api/mobile/v1/rutas/{payload.HojaDeRutaId}/paradas/{payload.ParadaId}/seleccionar";
                                var response = await client.PostAsJsonAsync(url, req, cancellationToken);
                                bool ok = await ProcesarRespuestaHttpAsync(op, response, "SeleccionarParada", detallesErrores, cancellationToken);
                                if (ok) exitosas++; else erroresNegocio++;
                            }
                            break;
                        }

                        case TiposOperacionOffline.EntregaExitosa:
                        {
                            var payload = JsonSerializer.Deserialize<EntregaExitosaOfflinePayload>(op.PayloadJson, JsonOptions);
                            if (payload != null)
                            {
                                if (payload.HojaDeRutaId == Guid.Empty) payload.HojaDeRutaId = DemoDataDefaults.DefaultHojaDeRutaId;
                                if (payload.ParadaId == Guid.Empty) payload.ParadaId = DemoDataDefaults.Parada2Id;
                                if (payload.EnvioId == Guid.Empty) payload.EnvioId = DemoDataDefaults.Envio2Id;
                                if (string.IsNullOrWhiteSpace(payload.IdempotencyKey)) payload.IdempotencyKey = $"ENTREGA-{payload.ParadaId:N}-{DateTime.UtcNow.Ticks}";
                                if (string.IsNullOrWhiteSpace(payload.NombreReceptor)) payload.NombreReceptor = "Lucía Fernández";
                                if (string.IsNullOrWhiteSpace(payload.DocumentoReceptor)) payload.DocumentoReceptor = "4.567.890-1";

                                var req = new
                                {
                                    EnvioId = payload.EnvioId,
                                    IdempotencyKey = payload.IdempotencyKey,
                                    NombreReceptor = payload.NombreReceptor,
                                    DocumentoReceptor = payload.DocumentoReceptor,
                                    FirmaDigitalBase64 = payload.FirmaDigitalBase64,
                                    FotoEvidenciaBase64 = payload.FotoEvidenciaBase64,
                                    Latitud = payload.Latitud,
                                    Longitud = payload.Longitud,
                                    FechaHoraDispositivoUtc = payload.FechaHoraDispositivoUtc
                                };

                                var url = $"/api/mobile/v1/rutas/{payload.HojaDeRutaId}/paradas/{payload.ParadaId}/entrega-exitosa";
                                var response = await client.PostAsJsonAsync(url, req, cancellationToken);

                                if (response.IsSuccessStatusCode)
                                {
                                    await _localDatabaseService.MarcarComoSincronizadoAsync(op.Id);
                                    await _localDatabaseService.ActualizarEstadoParadaLocalAsync(payload.ParadaId, "Entregado");
                                    exitosas++;
                                }
                                else
                                {
                                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                                    var detalle = $"Entrega Parada {payload.ParadaId:N} (Envío {payload.EnvioId:N}): HTTP {(int)response.StatusCode} - {error}";
                                    _logger.LogWarning("Error sincronizando EntregaExitosa: {Detalle}", detalle);
                                    await _localDatabaseService.RegistrarErrorAsync(op.Id, detalle);
                                    detallesErrores.Add(detalle);
                                    erroresNegocio++;
                                }
                            }
                            break;
                        }

                        case TiposOperacionOffline.FalloEntrega:
                        {
                            var payload = JsonSerializer.Deserialize<IntentoFallidoOfflinePayload>(op.PayloadJson, JsonOptions);
                            if (payload != null)
                            {
                                if (payload.HojaDeRutaId == Guid.Empty) payload.HojaDeRutaId = DemoDataDefaults.DefaultHojaDeRutaId;
                                if (payload.ParadaId == Guid.Empty) payload.ParadaId = DemoDataDefaults.Parada3Id;
                                if (payload.EnvioId == Guid.Empty) payload.EnvioId = DemoDataDefaults.Envio3Id;
                                if (string.IsNullOrWhiteSpace(payload.IdempotencyKey)) payload.IdempotencyKey = $"FALLO-{payload.ParadaId:N}-{DateTime.UtcNow.Ticks}";
                                if (string.IsNullOrWhiteSpace(payload.MotivoCodigo)) payload.MotivoCodigo = "CLI_AUSENTE";
                                if (!payload.MotivoNoEntregaId.HasValue || payload.MotivoNoEntregaId.Value == Guid.Empty)
                                    payload.MotivoNoEntregaId = Guid.Parse("dddddddd-0001-0000-0000-000000000001");
                                if (string.IsNullOrWhiteSpace(payload.Observaciones)) payload.Observaciones = "No atiende en domicilio";

                                var req = new
                                {
                                    EnvioId = payload.EnvioId,
                                    IdempotencyKey = payload.IdempotencyKey,
                                    MotivoNoEntregaId = payload.MotivoNoEntregaId,
                                    MotivoCodigo = payload.MotivoCodigo,
                                    Observaciones = payload.Observaciones,
                                    FotoEvidenciaBase64 = payload.FotoEvidenciaBase64,
                                    Latitud = payload.Latitud,
                                    Longitud = payload.Longitud,
                                    FechaHoraDispositivoUtc = payload.FechaHoraDispositivoUtc
                                };

                                var url = $"/api/mobile/v1/rutas/{payload.HojaDeRutaId}/paradas/{payload.ParadaId}/intento-fallido";
                                var response = await client.PostAsJsonAsync(url, req, cancellationToken);

                                if (response.IsSuccessStatusCode)
                                {
                                    await _localDatabaseService.MarcarComoSincronizadoAsync(op.Id);
                                    try
                                    {
                                        var resultDto = await response.Content.ReadFromJsonAsync<ResultadoIntentoFallidoDto>(cancellationToken: cancellationToken);
                                        if (resultDto != null && !string.IsNullOrWhiteSpace(resultDto.EstadoFinalResuelto))
                                        {
                                            await _localDatabaseService.ActualizarEstadoParadaLocalAsync(payload.ParadaId, resultDto.EstadoFinalResuelto);
                                        }
                                        else
                                        {
                                            await _localDatabaseService.ActualizarEstadoParadaLocalAsync(payload.ParadaId, "Fallido");
                                        }
                                    }
                                    catch
                                    {
                                        await _localDatabaseService.ActualizarEstadoParadaLocalAsync(payload.ParadaId, "Fallido");
                                    }

                                    exitosas++;
                                }
                                else
                                {
                                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                                    var detalle = $"Incidencia Parada {payload.ParadaId:N} (Envío {payload.EnvioId:N}): HTTP {(int)response.StatusCode} - {error}";
                                    _logger.LogWarning("Error sincronizando IntentoFallido: {Detalle}", detalle);
                                    await _localDatabaseService.RegistrarErrorAsync(op.Id, detalle);
                                    detallesErrores.Add(detalle);
                                    erroresNegocio++;
                                }
                            }
                            break;
                        }

                        default:
                            _logger.LogWarning("Tipo de operación offline no reconocido: {Tipo}", op.TipoOperacion);
                            var errorTipo = $"Op #{op.Id}: Tipo de operación '{op.TipoOperacion}' no soportado.";
                            await _localDatabaseService.RegistrarErrorAsync(op.Id, errorTipo);
                            detallesErrores.Add(errorTipo);
                            erroresNegocio++;
                            break;
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Corte de conectividad HTTP al procesar operación {Id}. Deteniendo sincronización.", op.Id);
                    detallesErrores.Add($"Op #{op.Id}: Corte de conexión ({ex.Message})");
                    detenidoPorCorteDeRed = true;
                    break;
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Timeout al procesar operación {Id}. Deteniendo sincronización.", op.Id);
                    detallesErrores.Add($"Op #{op.Id}: Timeout en comunicación con servidor.");
                    detenidoPorCorteDeRed = true;
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al procesar operación {Id}.", op.Id);
                    var detalleEx = $"Op #{op.Id}: Excepción local - {ex.Message}";
                    detallesErrores.Add(detalleEx);
                    await _localDatabaseService.RegistrarErrorAsync(op.Id, detalleEx);
                    erroresNegocio++;
                }
            }

            string mensaje;
            if (detenidoPorCorteDeRed)
            {
                mensaje = $"Sincronización interrumpida por pérdida de conexión.\n• Enviadas: {exitosas}\n• Pendientes con observación: {erroresNegocio}\n" + string.Join("\n", detallesErrores);
            }
            else if (erroresNegocio > 0)
            {
                mensaje = $"Sincronización finalizada con observaciones del servidor:\n• Exitosas: {exitosas}\n• Rechazadas por servidor: {erroresNegocio}\n\nDetalles del backend:\n" + string.Join("\n", detallesErrores);
            }
            else
            {
                mensaje = $"Sincronización exitosa: Las {exitosas} operaciones pendientes se sincronizaron con el servidor.";
            }

            return new SyncResult(procesadas, exitosas, erroresNegocio, detenidoPorCorteDeRed, mensaje, detallesErrores);
        }
        finally
        {
            IsSyncing = false;
            _syncSemaphore.Release();
            await NotificarCambioEstadoAsync();
        }
    }

    private async Task<bool> ProcesarRespuestaHttpAsync(OperacionOfflineLocal op, HttpResponseMessage response, string nombreOp, List<string> detallesErrores, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            await _localDatabaseService.MarcarComoSincronizadoAsync(op.Id);
            return true;
        }

        var error = await response.Content.ReadAsStringAsync(ct);
        var detalle = $"{nombreOp} (Op #{op.Id}): HTTP {(int)response.StatusCode} - {error}";
        _logger.LogWarning("Respuesta no exitosa en {Nombre}: {Detalle}", nombreOp, detalle);
        await _localDatabaseService.RegistrarErrorAsync(op.Id, detalle);
        detallesErrores.Add(detalle);
        return false;
    }

    public async Task<string> ReiniciarDemoCompletaAsync(CancellationToken cancellationToken = default)
    {
        string msgBackend = string.Empty;
        try
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsync("/api/mobile/v1/demo/reset", null, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                msgBackend = "Backend (PostgreSQL) reiniciado: ENV-2026-00108, ENV-2026-00109 y ENV-2026-00110 restaurados a EnTransito (4).\n";
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                msgBackend = $"Aviso backend: HTTP {(int)response.StatusCode} - {errorBody}\n";
            }
        }
        catch (Exception ex)
        {
            msgBackend = $"Aviso: No fue posible contactar backend ({ex.Message}). Continuando con reinicio local.\n";
        }

        await _localDatabaseService.ReiniciarDatosDemoLocalAsync();
        await NotificarCambioEstadoAsync("Datos de demo reiniciados local y remotamente.");

        return $"{msgBackend}Base SQLite local reiniciada con éxito (cola en 0).";
    }

    private async Task NotificarCambioEstadoAsync(string? mensaje = null)
    {
        try
        {
            int pendientes = await _localDatabaseService.ObtenerCantidadPendientesAsync();
            OnSyncStateChanged?.Invoke(this, new SyncStateChangedEventArgs
            {
                IsOnline = IsOnline,
                IsOfflineSimulado = IsOfflineSimulado,
                OperacionesPendientes = pendientes,
                IsSyncing = IsSyncing,
                Mensaje = mensaje
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al disparar notificación de cambio de estado de sincronización.");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
            _syncSemaphore.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
