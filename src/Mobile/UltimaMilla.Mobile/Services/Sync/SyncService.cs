using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UltimaMilla.Mobile.Configuration;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.DTOs;
using UltimaMilla.Mobile.Models.Local;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.Services.Auth;
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
    RutaActivaRepartidorDto? UltimaRutaDescargada { get; }

    event EventHandler<SyncStateChangedEventArgs>? OnSyncStateChanged;

    Task<List<ParadaItemViewModel>> DescargarRutaDelDiaAsync(Guid? hojaDeRutaId = null, CancellationToken cancellationToken = default);
    Task<SyncResult> SincronizarOperacionesPendientesAsync(CancellationToken cancellationToken = default);
    Task AlternarModoOfflineSimuladoAsync();
    Task<int> ObtenerCantidadPendientesAsync();

    /// <summary>
    /// Indica si el operador tiene habilitada una funcionalidad ("entrega-parcial", "retiro-en-domicilio"…), según la última consulta
    /// al servidor. Mientras no se haya podido consultar, se considera habilitada: no se oculta nada por falta de datos.
    /// </summary>
    bool FuncionalidadHabilitada(string clave);
}

public class SyncService : ISyncService, IDisposable
{
    private readonly ILocalDatabaseService _localDatabaseService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthService _authService;
    private readonly ILogger<SyncService> _logger;
    private readonly SemaphoreSlim _syncSemaphore = new(1, 1);

    private bool _isOfflineSimulado = false;
    private bool _isSyncing = false;
    private bool _disposed;

    public event EventHandler<SyncStateChangedEventArgs>? OnSyncStateChanged;

    public RutaActivaRepartidorDto? UltimaRutaDescargada { get; private set; }

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
        IAuthService authService,
        ILogger<SyncService> logger)
    {
        _localDatabaseService = localDatabaseService;
        _httpClientFactory = httpClientFactory;
        _authService = authService;
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

    private Dictionary<string, bool> _funcionalidades = new(StringComparer.OrdinalIgnoreCase);

    public bool FuncionalidadHabilitada(string clave) =>
        !_funcionalidades.TryGetValue(clave, out var habilitada) || habilitada;

    /// <summary>Consulta las funcionalidades activas del operador (GET /api/funcionalidades). Un fallo no interrumpe la descarga de la ruta.</summary>
    private async Task ActualizarFuncionalidadesAsync(HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            var respuesta = await client.GetFromJsonAsync<Dictionary<string, bool>>("/api/funcionalidades", JsonOptions, cancellationToken);
            if (respuesta is not null)
            {
                _funcionalidades = new Dictionary<string, bool>(respuesta, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron consultar las funcionalidades del operador; se mantienen las últimas conocidas.");
        }
    }

    /// <summary>
    /// Descarga dinámicamente la hoja de ruta activa del repartidor autenticado desde GET /api/mobile/v1/rutas/activa
    /// y la persiste en SQLite cifrado. Si el dispositivo está en modo offline o sin señal, recupera la última ruta sincronizada en SQLite.
    /// </summary>
    public async Task<List<ParadaItemViewModel>> DescargarRutaDelDiaAsync(Guid? hojaDeRutaId = null, CancellationToken cancellationToken = default)
    {
        if (IsOnline)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("BackendApi");
                client.BaseAddress = new Uri(ApiEndpoints.BaseUrl);

                await ActualizarFuncionalidadesAsync(client, cancellationToken);

                // 1. Consultar endpoint autenticado de ruta activa del repartidor
                var responseActiva = await client.GetAsync("/api/mobile/v1/rutas/activa", cancellationToken);
                if (responseActiva.IsSuccessStatusCode)
                {
                    var rutaActiva = await responseActiva.Content.ReadFromJsonAsync<RutaActivaRepartidorDto>(JsonOptions, cancellationToken);
                    if (rutaActiva != null && rutaActiva.HojaDeRutaId != Guid.Empty)
                    {
                        UltimaRutaDescargada = rutaActiva;
                        if (rutaActiva.RepartidorId != Guid.Empty)
                        {
                            await _authService.ActualizarRepartidorIdAsync(rutaActiva.RepartidorId);
                        }

                        List<MotivoNoEntregaLocal>? motivosLocal = null;
                        if (rutaActiva.MotivosNoEntrega is { Count: > 0 })
                        {
                            motivosLocal = rutaActiva.MotivosNoEntrega
                                .Select(m => new MotivoNoEntregaLocal
                                {
                                    Id = m.Id,
                                    Codigo = m.Codigo,
                                    Descripcion = m.Descripcion,
                                    Activo = true
                                })
                                .ToList();
                        }

                        await _localDatabaseService.GuardarRutaDescargadaAsync(
                            rutaActiva.HojaDeRutaId,
                            rutaActiva.Paradas,
                            motivosLocal,
                            estadoRuta: rutaActiva.Estado,
                            numeroRuta: rutaActiva.NumeroRuta);

                        _logger.LogInformation(
                            "Hoja de ruta activa {RutaId} descargada para el repartidor {RepartidorId} ({Count} paradas).",
                            rutaActiva.HojaDeRutaId,
                            rutaActiva.RepartidorId,
                            rutaActiva.Paradas.Count);

                        var localActualizado = await _localDatabaseService.ObtenerRutaActivaLocalAsync();
                        if (localActualizado != null)
                        {
                            await NotificarCambioEstadoAsync("Ruta activa sincronizada con el servidor.");
                            return localActualizado.Paradas;
                        }
                    }
                }
                else if (responseActiva.StatusCode == System.Net.HttpStatusCode.NotFound && !hojaDeRutaId.HasValue)
                {
                    _logger.LogInformation("El repartidor autenticado no posee una hoja de ruta activa en el servidor (404).");
                    // Si ya no tiene ruta (por ejemplo, la rindieron) y no queda nada por enviar, se descarta la copia local vieja
                    if (await _localDatabaseService.ObtenerCantidadPendientesAsync() == 0)
                    {
                        await _localDatabaseService.LimpiarDatosDeRutaAsync();
                    }
                    await NotificarCambioEstadoAsync("Sin hoja de ruta activa asignada.");
                    return new List<ParadaItemViewModel>();
                }
                else if (hojaDeRutaId.HasValue && hojaDeRutaId.Value != Guid.Empty)
                {
                    var url = $"/api/mobile/v1/rutas/{hojaDeRutaId.Value}/paradas";
                    var paradasDto = await client.GetFromJsonAsync<List<ParadaNavegacionDto>>(url, cancellationToken);
                    if (paradasDto != null && paradasDto.Count > 0)
                    {
                        await _localDatabaseService.GuardarRutaDescargadaAsync(hojaDeRutaId.Value, paradasDto);
                        var localActualizado = await _localDatabaseService.ObtenerRutaActivaLocalAsync();
                        if (localActualizado != null && localActualizado.Paradas.Count > 0)
                        {
                            await NotificarCambioEstadoAsync("Ruta actualizada desde backend.");
                            return localActualizado.Paradas;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No fue posible descargar la ruta activa desde el servidor. Utilizando almacenamiento local SQLite.");
            }
        }

        // Fallback Offline-First: Recuperar la última ruta sincronizada desde SQLite local cifrado
        var local = await _localDatabaseService.ObtenerRutaActivaLocalAsync();
        if (local != null && local.Paradas.Count > 0)
        {
            _logger.LogInformation("Cargando {Count} paradas desde base local SQLite cifrada.", local.Paradas.Count);
            await NotificarCambioEstadoAsync("Ruta cargada desde SQLite local.");
            return local.Paradas;
        }

        return new List<ParadaItemViewModel>();
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

            var rutaLocal = await _localDatabaseService.ObtenerRutaActivaLocalAsync();
            var rutaActivaId = rutaLocal?.Ruta?.Id ?? UltimaRutaDescargada?.HojaDeRutaId ?? Guid.Empty;

            _logger.LogInformation("Iniciando envío FIFO de {Count} operaciones pendientes...", pendientes.Count);
            var client = _httpClientFactory.CreateClient("BackendApi");
            client.BaseAddress = new Uri(ApiEndpoints.BaseUrl);

            var operacionesGps = pendientes
                .Where(o => o.TipoOperacion == TiposOperacionOffline.TelemetriaGps)
                .ToList();
            var otrasOperaciones = pendientes
                .Where(o => o.TipoOperacion != TiposOperacionOffline.TelemetriaGps)
                .ToList();

            // 1. Envío por lotes (batch) de hasta 500 lecturas GPS a POST /api/mobile/v1/posiciones
            if (operacionesGps.Count > 0 && IsOnline)
            {
                _logger.LogInformation("Agrupando {Count} lecturas GPS en lotes de hasta 500...", operacionesGps.Count);

                foreach (var lote in operacionesGps.Chunk(500))
                {
                    if (!IsOnline)
                    {
                        detenidoPorCorteDeRed = true;
                        break;
                    }

                    procesadas += lote.Length;
                    var listaPosiciones = new List<object>();
                    var idsLote = new List<int>();

                    foreach (var opGps in lote)
                    {
                        idsLote.Add(opGps.Id);
                        var payload = JsonSerializer.Deserialize<TelemetriaGpsOfflinePayload>(opGps.PayloadJson, JsonOptions);
                        if (payload != null)
                        {
                            var targetRutaId = payload.HojaDeRutaId != Guid.Empty ? payload.HojaDeRutaId : rutaActivaId;
                            listaPosiciones.Add(new
                            {
                                Latitud = payload.Latitud,
                                Longitud = payload.Longitud,
                                TomadaEn = (DateTime?)payload.TimestampUtc,
                                BateriaPct = payload.BateriaPct,
                                HojaDeRutaId = targetRutaId != Guid.Empty ? (Guid?)targetRutaId : null
                            });
                        }
                    }

                    if (listaPosiciones.Count > 0)
                    {
                        try
                        {
                            var reqBatch = new { Posiciones = listaPosiciones };
                            var response = await client.PostAsJsonAsync("/api/mobile/v1/posiciones", reqBatch, cancellationToken);
                            if (response.IsSuccessStatusCode)
                            {
                                await _localDatabaseService.MarcarComoSincronizadosAsync(idsLote);
                                exitosas += lote.Length;
                            }
                            else
                            {
                                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                                var detalle = $"TelemetriaGpsBatch ({lote.Length} pts): HTTP {(int)response.StatusCode} - {error}";
                                _logger.LogWarning("Error sincronizando lote de telemetría GPS: {Detalle}", detalle);
                                foreach (var id in idsLote)
                                {
                                    await _localDatabaseService.RegistrarErrorAsync(id, detalle);
                                }
                                detallesErrores.Add(detalle);
                                erroresNegocio += lote.Length;
                            }
                        }
                        catch (HttpRequestException ex)
                        {
                            detallesErrores.Add($"Lote GPS: Corte de conexión ({ex.Message})");
                            detenidoPorCorteDeRed = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error al sincronizar lote de telemetría GPS.");
                            detallesErrores.Add($"Lote GPS: Excepción local - {ex.Message}");
                            erroresNegocio += lote.Length;
                        }
                    }
                }
            }

            // 2. Procesar el resto de operaciones de negocio en orden FIFO
            foreach (var op in otrasOperaciones)
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
                                var targetRutaId = payload.HojaDeRutaId != Guid.Empty ? payload.HojaDeRutaId : rutaActivaId;
                                var req = new
                                {
                                    CodigosBultosCargados = payload.CodigosBultosCargados,
                                    IdempotencyKey = payload.IdempotencyKey,
                                    ObservacionesDiscrepancia = payload.ObservacionesDiscrepancia
                                };

                                var url = $"/api/mobile/v1/rutas/{targetRutaId}/carga/confirmar";
                                var response = await client.PostAsJsonAsync(url, req, cancellationToken);
                                bool ok = await ProcesarRespuestaHttpAsync(op, response, "ConfirmarCarga", detallesErrores, cancellationToken);
                                if (ok)
                                {
                                    // El despacho de la ruta lo hace el despachador desde el depósito: la app solo informa la carga
                                    exitosas++;
                                }
                                else
                                {
                                    erroresNegocio++;
                                }
                            }
                            break;
                        }

                        case TiposOperacionOffline.SeleccionarParada:
                        {
                            var payload = JsonSerializer.Deserialize<SeleccionarParadaOfflinePayload>(op.PayloadJson, JsonOptions);
                            if (payload != null)
                            {
                                var targetRutaId = payload.HojaDeRutaId != Guid.Empty ? payload.HojaDeRutaId : rutaActivaId;
                                var req = new
                                {
                                    LatitudActual = payload.LatitudActual,
                                    LongitudActual = payload.LongitudActual,
                                    Timestamp = payload.Timestamp,
                                    Accion = payload.Accion
                                };

                                var url = $"/api/mobile/v1/rutas/{targetRutaId}/paradas/{payload.ParadaId}/seleccionar";
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
                                if (payload.HojaDeRutaId == Guid.Empty) payload.HojaDeRutaId = rutaActivaId;
                                if (string.IsNullOrWhiteSpace(payload.IdempotencyKey)) payload.IdempotencyKey = $"ENTREGA-{payload.ParadaId:N}-{DateTime.UtcNow.Ticks}";

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
                                else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                                {
                                    var detalle = $"Entrega Parada {payload.ParadaId:N} (Envío {payload.EnvioId:N}): Conflicto resuelto por el servidor (409 Conflict).";
                                    _logger.LogInformation("Conflicto 409 resuelto en EntregaExitosa: {Detalle}", detalle);
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
                                if (payload.HojaDeRutaId == Guid.Empty) payload.HojaDeRutaId = rutaActivaId;
                                if (string.IsNullOrWhiteSpace(payload.IdempotencyKey)) payload.IdempotencyKey = $"FALLO-{payload.ParadaId:N}-{DateTime.UtcNow.Ticks}";
                                if (string.IsNullOrWhiteSpace(payload.MotivoCodigo)) payload.MotivoCodigo = "CLI_AUSENTE";
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
                                            if (resultDto.ConflictoResuelto)
                                            {
                                                _logger.LogInformation("Conflicto resuelto por el servidor para parada {ParadaId}: {Mensaje}", payload.ParadaId, resultDto.Mensaje);
                                            }
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
                                else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                                {
                                    var detalle = $"Incidencia Parada {payload.ParadaId:N} (Envío {payload.EnvioId:N}): Conflicto 409 resuelto por el servidor.";
                                    _logger.LogInformation("Conflicto 409 resuelto en IntentoFallido: {Detalle}", detalle);
                                    await _localDatabaseService.MarcarComoSincronizadoAsync(op.Id);
                                    await _localDatabaseService.ActualizarEstadoParadaLocalAsync(payload.ParadaId, "Reasignado");
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

                        case TiposOperacionOffline.EntregaParcial:
                        {
                            var payload = JsonSerializer.Deserialize<EntregaParcialOfflinePayload>(op.PayloadJson, JsonOptions);
                            if (payload != null)
                            {
                                if (payload.HojaDeRutaId == Guid.Empty) payload.HojaDeRutaId = rutaActivaId;

                                var req = new
                                {
                                    EnvioId = payload.EnvioId,
                                    IdempotencyKey = payload.IdempotencyKey,
                                    CodigosBultosEntregados = payload.CodigosBultosEntregados,
                                    BultosNoEntregados = payload.BultosNoEntregados.Select(b => new
                                    {
                                        CodigoBulto = b.CodigoBulto,
                                        MotivoCodigo = b.MotivoCodigo,
                                        MotivoNoEntregaId = b.MotivoNoEntregaId,
                                        Observaciones = b.Observaciones
                                    }).ToList(),
                                    NombreReceptor = payload.NombreReceptor,
                                    DocumentoReceptor = payload.DocumentoReceptor,
                                    FirmaDigitalBase64 = payload.FirmaDigitalBase64,
                                    FotoEvidenciaBase64 = payload.FotoEvidenciaBase64,
                                    Latitud = payload.Latitud,
                                    Longitud = payload.Longitud,
                                    FechaHoraDispositivoUtc = payload.FechaHoraDispositivoUtc
                                };

                                var url = $"/api/mobile/v1/rutas/{payload.HojaDeRutaId}/paradas/{payload.ParadaId}/entrega-parcial";
                                var response = await client.PostAsJsonAsync(url, req, cancellationToken);
                                bool ok = await ProcesarRespuestaHttpAsync(op, response, "EntregaParcial", detallesErrores, cancellationToken);
                                if (ok) exitosas++; else erroresNegocio++;
                            }
                            break;
                        }

                        case TiposOperacionOffline.RetiroRealizado:
                        {
                            var payload = JsonSerializer.Deserialize<RetiroRealizadoOfflinePayload>(op.PayloadJson, JsonOptions);
                            if (payload != null)
                            {
                                if (payload.HojaDeRutaId == Guid.Empty) payload.HojaDeRutaId = rutaActivaId;

                                var req = new
                                {
                                    RetiroId = payload.RetiroId,
                                    IdempotencyKey = payload.IdempotencyKey,
                                    CodigosBultosEscaneados = payload.CodigosBultosEscaneados,
                                    FotoEstadoBase64 = payload.FotoEstadoBase64,
                                    FirmaBase64 = payload.FirmaBase64,
                                    NombreEntrega = payload.NombreEntrega,
                                    DocumentoEntrega = payload.DocumentoEntrega,
                                    Latitud = payload.Latitud,
                                    Longitud = payload.Longitud,
                                    FechaHoraDispositivoUtc = payload.FechaHoraDispositivoUtc
                                };

                                var url = $"/api/mobile/v1/rutas/{payload.HojaDeRutaId}/paradas/{payload.ParadaId}/retiro";
                                var response = await client.PostAsJsonAsync(url, req, cancellationToken);
                                bool ok = await ProcesarRespuestaHttpAsync(op, response, "RetiroRealizado", detallesErrores, cancellationToken);
                                if (ok)
                                {
                                    // Si el servidor cambió el retiro mientras no había conexión, prevalece lo hecho en el domicilio (conflictoResuelto)
                                    exitosas++;
                                }
                                else
                                {
                                    erroresNegocio++;
                                }
                            }
                            break;
                        }

                        case TiposOperacionOffline.RetiroNoRealizado:
                        {
                            var payload = JsonSerializer.Deserialize<RetiroNoRealizadoOfflinePayload>(op.PayloadJson, JsonOptions);
                            if (payload != null)
                            {
                                if (payload.HojaDeRutaId == Guid.Empty) payload.HojaDeRutaId = rutaActivaId;

                                var req = new
                                {
                                    RetiroId = payload.RetiroId,
                                    IdempotencyKey = payload.IdempotencyKey,
                                    MotivoCodigo = payload.MotivoCodigo,
                                    MotivoNoEntregaId = payload.MotivoNoEntregaId,
                                    Observaciones = payload.Observaciones,
                                    Latitud = payload.Latitud,
                                    Longitud = payload.Longitud,
                                    FechaHoraDispositivoUtc = payload.FechaHoraDispositivoUtc
                                };

                                var url = $"/api/mobile/v1/rutas/{payload.HojaDeRutaId}/paradas/{payload.ParadaId}/retiro/no-realizado";
                                var response = await client.PostAsJsonAsync(url, req, cancellationToken);
                                bool ok = await ProcesarRespuestaHttpAsync(op, response, "RetiroNoRealizado", detallesErrores, cancellationToken);
                                if (ok) exitosas++; else erroresNegocio++;
                            }
                            break;
                        }

                        case TiposOperacionOffline.RetiroDevolucion:
                        {
                            var payload = JsonSerializer.Deserialize<RetiroDevolucionOfflinePayload>(op.PayloadJson, JsonOptions);
                            if (payload != null)
                            {
                                if (payload.HojaDeRutaId == Guid.Empty) payload.HojaDeRutaId = rutaActivaId;
                                if (string.IsNullOrWhiteSpace(payload.IdempotencyKey)) payload.IdempotencyKey = $"RET-DEV-{payload.ParadaId:N}-{DateTime.UtcNow.Ticks}";

                                var req = new
                                {
                                    EnvioId = payload.EnvioId,
                                    IdempotencyKey = payload.IdempotencyKey,
                                    Latitud = payload.Latitud,
                                    Longitud = payload.Longitud,
                                    FechaHoraDispositivoUtc = payload.FechaHoraDispositivoUtc ?? DateTime.UtcNow,
                                    Observaciones = payload.Observaciones
                                };

                                var url = $"/api/mobile/v1/rutas/{payload.HojaDeRutaId}/paradas/{payload.ParadaId}/retiro-devolucion";
                                var response = await client.PostAsJsonAsync(url, req, cancellationToken);

                                if (response.IsSuccessStatusCode)
                                {
                                    await _localDatabaseService.MarcarComoSincronizadoAsync(op.Id);
                                    await _localDatabaseService.ActualizarEstadoParadaLocalAsync(payload.ParadaId, "EnDevolucion");
                                    exitosas++;
                                }
                                else
                                {
                                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                                    var detalle = $"Retiro Devolución Parada {payload.ParadaId:N} (Envío {payload.EnvioId:N}): HTTP {(int)response.StatusCode} - {error}";
                                    _logger.LogWarning("Error sincronizando RetiroDevolucion: {Detalle}", detalle);
                                    await _localDatabaseService.RegistrarErrorAsync(op.Id, detalle);
                                    detallesErrores.Add(detalle);
                                    erroresNegocio++;
                                }
                            }
                            break;
                        }

                        case TiposOperacionOffline.EventoGeocerca:
                        {
                            var payload = JsonSerializer.Deserialize<EventoGeocercaOfflinePayload>(op.PayloadJson, JsonOptions);
                            if (payload != null)
                            {
                                if (payload.HojaDeRutaId == Guid.Empty) payload.HojaDeRutaId = rutaActivaId;
                                if (string.IsNullOrWhiteSpace(payload.IdempotencyKey))
                                    payload.IdempotencyKey = $"GEO-{payload.TipoEvento}-{payload.ParadaId:N}-{payload.TimestampUtc.Ticks}";

                                var req = new
                                {
                                    EnvioId = payload.EnvioId,
                                    TipoEvento = payload.TipoEvento,
                                    Latitud = payload.Latitud,
                                    Longitud = payload.Longitud,
                                    DistanciaMetros = payload.DistanciaMetros,
                                    TiempoPermanenciaSegundos = payload.TiempoPermanenciaSegundos,
                                    TimestampUtc = payload.TimestampUtc,
                                    IdempotencyKey = payload.IdempotencyKey
                                };

                                var url = $"/api/mobile/v1/rutas/{payload.HojaDeRutaId}/paradas/{payload.ParadaId}/geocerca";
                                var response = await client.PostAsJsonAsync(url, req, cancellationToken);
                                bool ok = await ProcesarRespuestaHttpAsync(op, response, "EventoGeocerca", detallesErrores, cancellationToken);
                                if (ok)
                                {
                                    if (payload.EventoLocalId.HasValue)
                                    {
                                        await _localDatabaseService.MarcarEventoGeocercaSincronizadoAsync(payload.EventoLocalId.Value);
                                    }
                                    exitosas++;
                                }
                                else
                                {
                                    erroresNegocio++;
                                }
                            }
                            break;
                        }

                        case TiposOperacionOffline.IniciarRecorrido:
                        {
                            var payload = JsonSerializer.Deserialize<IniciarRecorridoOfflinePayload>(op.PayloadJson, JsonOptions);
                            if (payload != null)
                            {
                                var targetRutaId = payload.HojaDeRutaId != Guid.Empty ? payload.HojaDeRutaId : rutaActivaId;
                                var req = new
                                {
                                    IdempotencyKey = payload.IdempotencyKey
                                };

                                var url = $"/api/mobile/v1/rutas/{targetRutaId}/iniciar-recorrido";
                                var response = await client.PostAsJsonAsync(url, req, cancellationToken);
                                bool ok = await ProcesarRespuestaHttpAsync(op, response, "IniciarRecorrido", detallesErrores, cancellationToken);
                                if (ok)
                                {
                                    await _localDatabaseService.ActualizarEstadoHojaDeRutaAsync(targetRutaId, "EnTransito");
                                    exitosas++;
                                }
                                else
                                {
                                    erroresNegocio++;
                                }
                            }
                            break;
                        }

                        case TiposOperacionOffline.TelemetriaGps:
                        {
                            var payload = JsonSerializer.Deserialize<TelemetriaGpsOfflinePayload>(op.PayloadJson, JsonOptions);
                            if (payload != null)
                            {
                                var targetRutaId = payload.HojaDeRutaId != Guid.Empty ? payload.HojaDeRutaId : rutaActivaId;
                                var req = new
                                {
                                    Posiciones = new[]
                                    {
                                        new
                                        {
                                            Latitud = payload.Latitud,
                                            Longitud = payload.Longitud,
                                            TomadaEn = (DateTime?)payload.TimestampUtc,
                                            BateriaPct = payload.BateriaPct,
                                            HojaDeRutaId = targetRutaId != Guid.Empty ? (Guid?)targetRutaId : null
                                        }
                                    }
                                };

                                var url = "/api/mobile/v1/posiciones";
                                var response = await client.PostAsJsonAsync(url, req, cancellationToken);
                                bool ok = await ProcesarRespuestaHttpAsync(op, response, "TelemetriaGps", detallesErrores, cancellationToken);
                                if (ok)
                                {
                                    exitosas++;
                                }
                                else
                                {
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
                mensaje = $"Sincronización interrumpida por pérdida de conexión.\n- Enviadas: {exitosas}\n- Pendientes con observación: {erroresNegocio}\n" + string.Join("\n", detallesErrores);
            }
            else if (erroresNegocio > 0)
            {
                mensaje = $"Sincronización finalizada con observaciones del servidor:\n- Exitosas: {exitosas}\n- Rechazadas por servidor: {erroresNegocio}\n\nDetalles del backend:\n" + string.Join("\n", detallesErrores);
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
