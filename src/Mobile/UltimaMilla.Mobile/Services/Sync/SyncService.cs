using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UltimaMilla.Mobile.Configuration;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.DTOs;
using UltimaMilla.Mobile.Models.Local;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.Services.Auth;
using UltimaMilla.Mobile.Services.Notifications;
using UltimaMilla.Mobile.Services.Theme;
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
    Task<List<RutaActivaRepartidorDto>> DescargarYActualizarRutasDelServidorAsync(CancellationToken cancellationToken = default);
    Task<bool> FinalizarHojaDeRutaAsync(Guid hojaDeRutaId, List<string>? codigosBultosDevueltos = null, string? observaciones = null, CancellationToken cancellationToken = default);
    Task<List<EnvioAdmitidoMobileDto>> ObtenerEnviosAdmitidosDepositoAsync(CancellationToken cancellationToken = default);
    Task<bool> RecepcionarBultosDepositoAsync(Guid envioId, string codigoSeguimiento, List<string> codigosEscaneados, List<string>? codigosDanados = null, string? tipoDiscrepancia = null, string? observaciones = null, CancellationToken cancellationToken = default);
    Task<SyncResult> SincronizarOperacionesPendientesAsync(CancellationToken cancellationToken = default);
    Task AlternarModoOfflineSimuladoAsync();
    Task<int> ObtenerCantidadPendientesAsync();

    /// <summary>
    /// Indica si el operador tiene habilitada una funcionalidad ("entrega-parcial", "retiro-en-domicilio"…), según la última consulta
    /// al servidor. Mientras no se haya podido consultar, se considera habilitada: no se oculta nada por falta de datos.
    /// </summary>
    bool FuncionalidadHabilitada(string clave);

    /// <summary>
    /// Registra que ya se emitio una notificacion operativa reciente para una hoja de ruta (evita duplicados entre SignalR y polling).
    /// </summary>
    void RegistrarNotificacionRutaEmitida(Guid hojaDeRutaId, string tipoEvento) { }
}

public class SyncService : ISyncService, IDisposable
{
    private readonly ILocalDatabaseService _localDatabaseService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthService _authService;
    private readonly ILogger<SyncService> _logger;
    private readonly IOperadorThemeService? _themeService;
    private readonly INotificacionLocalService? _notificacionService;
    private readonly SemaphoreSlim _syncSemaphore = new(1, 1);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _alertasRutaEmitidas = new();

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
        ILogger<SyncService> logger,
        IOperadorThemeService? themeService = null,
        INotificacionLocalService? notificacionService = null)
    {
        _localDatabaseService = localDatabaseService;
        _httpClientFactory = httpClientFactory;
        _authService = authService;
        _logger = logger;
        _themeService = themeService;
        _notificacionService = notificacionService;

        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    public void RegistrarNotificacionRutaEmitida(Guid hojaDeRutaId, string tipoEvento)
    {
        if (hojaDeRutaId == Guid.Empty || string.IsNullOrWhiteSpace(tipoEvento))
        {
            return;
        }

        _alertasRutaEmitidas[$"{hojaDeRutaId}:{tipoEvento.Trim().ToUpperInvariant()}"] = DateTimeOffset.UtcNow;
    }

    private bool FueNotificadaRecientemente(Guid hojaDeRutaId, string tipoEvento)
    {
        var clave = $"{hojaDeRutaId}:{tipoEvento.Trim().ToUpperInvariant()}";
        return _alertasRutaEmitidas.TryGetValue(clave, out var previo)
            && (DateTimeOffset.UtcNow - previo) < TimeSpan.FromSeconds(30);
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
                    var resultado = await SincronizarOperacionesPendientesAsync();
                    if (resultado.Exitosas > 0 && _notificacionService is not null)
                    {
                        await _notificacionService.MostrarNotificacionOperativaAsync(
                            "Sincronización Offline Completada",
                            $"Se transmitieron {resultado.Exitosas} operaciones pendientes a la central correctamente.",
                            "SINCRONIZACION");
                    }

                    await DescargarYActualizarRutasDelServidorAsync();
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
    /// Consulta y descarga todas las hojas de ruta asignadas al repartidor desde el backend (GET /api/mobile/v1/rutas/activas).
    /// Upsertea cada ruta y sus paradas en SQLite local cifrado.
    /// </summary>
    public async Task<List<RutaActivaRepartidorDto>> DescargarYActualizarRutasDelServidorAsync(CancellationToken cancellationToken = default)
    {
        var rutasDescargadas = new List<RutaActivaRepartidorDto>();

        if (IsOnline)
        {
            try
            {
                if (_themeService != null)
                {
                    await _themeService.SincronizarIdentidadVisualAsync(cancellationToken);
                }

                var client = _httpClientFactory.CreateClient("BackendApi");
                client.BaseAddress = new Uri(ApiEndpoints.BaseUrl);

                await ActualizarFuncionalidadesAsync(client, cancellationToken);

                // 1. Intentar consultar endpoint que devuelve todas las rutas activas/vigentes del repartidor: GET /api/mobile/v1/rutas/activas
                bool servidorRespondioLista = false;
                var responseActivas = await client.GetAsync("/api/mobile/v1/rutas/activas", cancellationToken);
                if (responseActivas.IsSuccessStatusCode)
                {
                    var lista = await responseActivas.Content.ReadFromJsonAsync<List<RutaActivaRepartidorDto>>(JsonOptions, cancellationToken);
                    if (lista != null)
                    {
                        servidorRespondioLista = true;
                        if (lista.Count > 0)
                        {
                            rutasDescargadas.AddRange(lista);
                        }
                        else if (await _localDatabaseService.ObtenerCantidadPendientesAsync() == 0)
                        {
                            await _localDatabaseService.LimpiarDatosDeRutaAsync();
                        }
                    }
                }
                else if (responseActivas.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Fallback de compatibilidad: consultar GET /api/mobile/v1/rutas/activa
                    var responseActiva = await client.GetAsync("/api/mobile/v1/rutas/activa", cancellationToken);
                    if (responseActiva.IsSuccessStatusCode)
                    {
                        var rutaUnica = await responseActiva.Content.ReadFromJsonAsync<RutaActivaRepartidorDto>(JsonOptions, cancellationToken);
                        if (rutaUnica != null && rutaUnica.HojaDeRutaId != Guid.Empty)
                        {
                            rutasDescargadas.Add(rutaUnica);
                        }
                    }
                    else if (responseActiva.StatusCode == System.Net.HttpStatusCode.NotFound && await _localDatabaseService.ObtenerCantidadPendientesAsync() == 0)
                    {
                        await _localDatabaseService.LimpiarDatosDeRutaAsync();
                    }
                }

                var rutasPrevias = (await _localDatabaseService.ListarHojasDeRutaLocalesAsync())
                    .ToDictionary(h => h.Id);

                // Guardar cada ruta descargada en SQLite local y detectar novedades operativas
                foreach (var r in rutasDescargadas)
                {
                    if (r.HojaDeRutaId == Guid.Empty) continue;

                    rutasPrevias.TryGetValue(r.HojaDeRutaId, out var rutaPrevia);
                    List<ParadaItemViewModel>? paradasPrevias = null;
                    if (rutaPrevia is not null && !EsEstadoTerminalRuta(rutaPrevia.Estado) && !EsEstadoTerminalRuta(r.Estado))
                    {
                        var rutaLocalActual = await _localDatabaseService.ObtenerRutaActivaLocalAsync(r.HojaDeRutaId);
                        paradasPrevias = rutaLocalActual?.Paradas;
                    }

                    UltimaRutaDescargada = r;
                    if (!string.IsNullOrWhiteSpace(r.TipoPruebaEntregaExigida))
                    {
                        Preferences.Default.Set("ReglaOperativa_TipoPruebaEntrega", r.TipoPruebaEntregaExigida);
                    }
                    if (r.MaxIntentosEntrega.HasValue)
                    {
                        Preferences.Default.Set("ReglaOperativa_MaxIntentos", r.MaxIntentosEntrega.Value);
                    }
                    if (r.RepartidorId != Guid.Empty)
                    {
                        await _authService.ActualizarRepartidorIdAsync(r.RepartidorId);
                    }

                    List<MotivoNoEntregaLocal>? motivosLocal = null;
                    if (r.MotivosNoEntrega is { Count: > 0 })
                    {
                        motivosLocal = r.MotivosNoEntrega
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
                        r.HojaDeRutaId,
                        r.Paradas,
                        motivosLocal,
                        estadoRuta: r.Estado,
                        numeroRuta: r.NumeroRuta,
                        fechaRuta: r.Fecha.ToDateTime(TimeOnly.MinValue));

                    if (EsEstadoTerminalRuta(r.Estado))
                    {
                        await _localDatabaseService.PurgarOperacionesPorRutaAsync(r.HojaDeRutaId);
                    }
                    else
                    {
                        await DetectarYNotificarCambiosDeRutaAsync(r, rutaPrevia, paradasPrevias);
                    }
                }

                if (servidorRespondioLista)
                {
                    await _localDatabaseService.MarcarRutasAusentesComoRendidasAsync(
                        rutasDescargadas.Select(r => r.HojaDeRutaId));
                }

                if (rutasDescargadas.Count > 0)
                {
                    _logger.LogInformation("Se descargaron y actualizaron {Count} hojas de ruta desde el servidor.", rutasDescargadas.Count);
                    await NotificarCambioEstadoAsync($"Se actualizaron {rutasDescargadas.Count} rutas desde el servidor.");
                }

                // Sincronizar también la identidad visual del operador en caso de cambios desde el Backoffice
                if (_themeService != null)
                {
                    await _themeService.SincronizarIdentidadVisualAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fallo al consultar rutas activas desde el servidor.");
            }
        }

        return rutasDescargadas;
    }

    private static bool EsEstadoTerminalRuta(string? estado) =>
        estado is not null && (
            estado.Equals("Rendida", StringComparison.OrdinalIgnoreCase) ||
            estado.Equals("Finalizada", StringComparison.OrdinalIgnoreCase) ||
            estado.Equals("Cerrada", StringComparison.OrdinalIgnoreCase));

    private async Task DetectarYNotificarCambiosDeRutaAsync(
        RutaActivaRepartidorDto rutaActual,
        HojaDeRutaLocal? rutaPrevia,
        List<ParadaItemViewModel>? paradasPrevias)
    {
        if (_notificacionService is null)
        {
            return;
        }

        if (rutaPrevia is null)
        {
            if (FueNotificadaRecientemente(rutaActual.HojaDeRutaId, "NUEVA_RUTA"))
            {
                return;
            }

            RegistrarNotificacionRutaEmitida(rutaActual.HojaDeRutaId, "NUEVA_RUTA");
            var codigoRuta = string.IsNullOrWhiteSpace(rutaActual.NumeroRuta)
                ? $"HR-{rutaActual.Fecha:yyyyMMdd}-01"
                : rutaActual.NumeroRuta.Trim();
            var totalPendientes = rutaActual.Paradas.Count(p =>
                !string.Equals(p.EstadoParada, "Entregado", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.EstadoParada, "Cancelado", StringComparison.OrdinalIgnoreCase));

            await _notificacionService.MostrarNotificacionOperativaAsync(
                "Nueva Hoja de Ruta Asignada",
                $"Se habilito la ruta {codigoRuta} con {totalPendientes} paradas pendientes. Ingresa a la seccion Carga para verificar los bultos.",
                "RUTA");
            return;
        }

        if (paradasPrevias is null)
        {
            return;
        }

        var detalleCambio = DescribirCambioDeParadas(paradasPrevias, rutaActual.Paradas);
        if (detalleCambio is null || FueNotificadaRecientemente(rutaActual.HojaDeRutaId, "RUTA_ACTUALIZADA"))
        {
            return;
        }

        RegistrarNotificacionRutaEmitida(rutaActual.HojaDeRutaId, "RUTA_ACTUALIZADA");
        await _notificacionService.MostrarNotificacionOperativaAsync(
            "Actualizacion en Hoja de Ruta Activa",
            $"Se actualizaron las paradas de tu ruta en curso ({detalleCambio}). Revisa el listado actualizado.",
            "RUTA");
    }

    internal static string? DescribirCambioDeParadas(
        IReadOnlyList<ParadaItemViewModel> previas,
        IReadOnlyList<ParadaNavegacionDto> actuales)
    {
        var mapaPrevias = previas.ToDictionary(p => p.Id);
        var mapaActuales = actuales.ToDictionary(p => p.ParadaId);

        int agregadas = actuales.Count(a => !mapaPrevias.ContainsKey(a.ParadaId));
        int removidas = previas.Count(p => !mapaActuales.ContainsKey(p.Id));
        int canceladas = 0;
        int reprogramadas = 0;

        foreach (var actual in actuales)
        {
            if (!mapaPrevias.TryGetValue(actual.ParadaId, out var previa))
            {
                continue;
            }

            if (!string.Equals(previa.Estado, "Cancelado", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actual.EstadoParada, "Cancelado", StringComparison.OrdinalIgnoreCase))
            {
                canceladas++;
            }

            var ventanaPrevia = previa.VentanaHoraria?.Trim() ?? string.Empty;
            var ventanaActual = actual.VentanaHoraria?.Trim() ?? string.Empty;
            if (!string.Equals(ventanaPrevia, ventanaActual, StringComparison.OrdinalIgnoreCase))
            {
                reprogramadas++;
            }
        }

        var partes = new List<string>();
        if (agregadas > 0) partes.Add($"{agregadas} parada(s) agregada(s)");
        if (removidas > 0) partes.Add($"{removidas} parada(s) desasignada(s)");
        if (canceladas > 0) partes.Add($"{canceladas} parada(s) cancelada(s)");
        if (reprogramadas > 0) partes.Add($"{reprogramadas} parada(s) reprogramada(s)");

        return partes.Count > 0 ? string.Join(", ", partes) : null;
    }

    /// <summary>
    /// Descarga dinámicamente las hojas de ruta activas del repartidor y devuelve las paradas de la ruta solicitada o activa.
    /// Si el dispositivo está en modo offline o sin señal, recupera la ruta desde SQLite local cifrado.
    /// </summary>
    public async Task<List<ParadaItemViewModel>> DescargarRutaDelDiaAsync(Guid? hojaDeRutaId = null, CancellationToken cancellationToken = default)
    {
        if (IsOnline)
        {
            await DescargarYActualizarRutasDelServidorAsync(cancellationToken);
        }

        // Recuperar la ruta solicitada o activa por defecto desde SQLite local cifrado
        var local = await _localDatabaseService.ObtenerRutaActivaLocalAsync(hojaDeRutaId);
        if (local != null && local.Paradas.Count > 0)
        {
            _logger.LogInformation("Cargando {Count} paradas de la hoja de ruta {NumeroRuta} desde base local SQLite cifrada.", local.Paradas.Count, local.Ruta.NumeroRuta);
            await NotificarCambioEstadoAsync($"Ruta {local.Ruta.NumeroRuta} cargada ({local.Paradas.Count} paradas).");
            return local.Paradas;
        }

        return new List<ParadaItemViewModel>();
    }

    /// <summary>
    /// Finaliza la hoja de ruta en el dispositivo (retorno a depósito) y notifica al servidor (POST /api/mobile/v1/rutas/{id}/finalizar).
    /// Si está offline, se persiste localmente y se encola la operación en la cola FIFO.
    /// </summary>
    public async Task<bool> FinalizarHojaDeRutaAsync(
        Guid hojaDeRutaId,
        List<string>? codigosBultosDevueltos = null,
        string? observaciones = null,
        CancellationToken cancellationToken = default)
    {
        await _localDatabaseService.FinalizarHojaDeRutaLocalAsync(hojaDeRutaId);

        var idempotencyKey = $"FIN-RUTA-{hojaDeRutaId:N}-{DateTime.UtcNow.Ticks}";
        var payload = new FinalizarHojaDeRutaOfflinePayload
        {
            HojaDeRutaId = hojaDeRutaId,
            CodigosBultosDevueltos = codigosBultosDevueltos,
            Observaciones = observaciones,
            IdempotencyKey = idempotencyKey,
            FechaFinalizacionUtc = DateTime.UtcNow
        };

        bool sincronizadoOnline = false;

        if (IsOnline)
        {
            try
            {
                if (_themeService != null)
                {
                    await _themeService.SincronizarIdentidadVisualAsync(cancellationToken);
                }

                var client = _httpClientFactory.CreateClient("BackendApi");
                client.BaseAddress = new Uri(ApiEndpoints.BaseUrl);

                var reqBody = new
                {
                    CodigosBultosDevueltos = codigosBultosDevueltos,
                    Observaciones = observaciones
                };

                var response = await client.PostAsJsonAsync($"/api/mobile/v1/rutas/{hojaDeRutaId}/finalizar", reqBody, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    sincronizadoOnline = true;
                    _logger.LogInformation("Hoja de ruta {HojaDeRutaId} finalizada y sincronizada con el backend.", hojaDeRutaId);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var responseRendicion = await client.PostAsJsonAsync($"/api/mobile/v1/rutas/{hojaDeRutaId}/rendicion", reqBody, cancellationToken);
                    if (responseRendicion.IsSuccessStatusCode)
                    {
                        sincronizadoOnline = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo sincronizar la finalización de ruta en línea. Se encolará para sincronización posterior.");
            }
        }

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var opId = await _localDatabaseService.EncolarOperacionAsync(TiposOperacionOffline.FinalizarHojaDeRuta, json);
        if (sincronizadoOnline)
        {
            await _localDatabaseService.MarcarComoSincronizadoAsync(opId);
        }

        await NotificarCambioEstadoAsync("Hoja de ruta finalizada.");
        return true;
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
            var rutasRendidasEnLote = new HashSet<Guid>();
            foreach (var op in otrasOperaciones)
            {
                if (rutasRendidasEnLote.Any(id =>
                    !string.IsNullOrWhiteSpace(op.PayloadJson) &&
                    (op.PayloadJson.Contains(id.ToString("D"), StringComparison.OrdinalIgnoreCase) ||
                     op.PayloadJson.Contains(id.ToString("N"), StringComparison.OrdinalIgnoreCase))))
                {
                    await _localDatabaseService.PurgarOperacionOfflineAsync(op.Id);
                    continue;
                }
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
                                    var (ok, _, yaEntregado) = await ManejarErrorHttpOperacionAsync(
                                        op,
                                        response,
                                        $"Entrega Parada {payload.ParadaId:N} (Envío {payload.EnvioId:N})",
                                        payload.HojaDeRutaId,
                                        detallesErrores,
                                        rutasRendidasEnLote,
                                        cancellationToken);

                                    if (ok)
                                    {
                                        if (yaEntregado)
                                            await _localDatabaseService.ActualizarEstadoParadaLocalAsync(payload.ParadaId, "Entregado");
                                        exitosas++;
                                    }
                                    else
                                    {
                                        erroresNegocio++;
                                    }
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
                                    var (ok, _, _) = await ManejarErrorHttpOperacionAsync(
                                        op,
                                        response,
                                        $"Incidencia Parada {payload.ParadaId:N} (Envío {payload.EnvioId:N})",
                                        payload.HojaDeRutaId,
                                        detallesErrores,
                                        rutasRendidasEnLote,
                                        cancellationToken);
                                    if (ok) exitosas++; else erroresNegocio++;
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
                                    var (ok, _, _) = await ManejarErrorHttpOperacionAsync(
                                        op,
                                        response,
                                        $"Retiro Devolución Parada {payload.ParadaId:N} (Envío {payload.EnvioId:N})",
                                        payload.HojaDeRutaId,
                                        detallesErrores,
                                        rutasRendidasEnLote,
                                        cancellationToken);
                                    if (ok) exitosas++; else erroresNegocio++;
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
                                if (response.IsSuccessStatusCode)
                                {
                                    await _localDatabaseService.MarcarComoSincronizadoAsync(op.Id);
                                    await _localDatabaseService.ActualizarEstadoHojaDeRutaAsync(targetRutaId, "EnTransito");
                                    exitosas++;
                                }
                                else
                                {
                                    var (ok, rutaTerminada, _) = await ManejarErrorHttpOperacionAsync(
                                        op,
                                        response,
                                        "IniciarRecorrido",
                                        targetRutaId,
                                        detallesErrores,
                                        rutasRendidasEnLote,
                                        cancellationToken);
                                    if (ok)
                                    {
                                        if (!rutaTerminada)
                                            await _localDatabaseService.ActualizarEstadoHojaDeRutaAsync(targetRutaId, "EnTransito");
                                        exitosas++;
                                    }
                                    else
                                    {
                                        erroresNegocio++;
                                    }
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
                                            VelocidadKmh = payload.VelocidadKmh,
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

                        case TiposOperacionOffline.FinalizarHojaDeRuta:
                        {
                            var payload = JsonSerializer.Deserialize<FinalizarHojaDeRutaOfflinePayload>(op.PayloadJson, JsonOptions);
                            if (payload != null)
                            {
                                var targetRutaId = payload.HojaDeRutaId != Guid.Empty ? payload.HojaDeRutaId : rutaActivaId;
                                var req = new
                                {
                                    CodigosBultosDevueltos = payload.CodigosBultosDevueltos,
                                    MotivoInternoCodigo = payload.MotivoInternoCodigo,
                                    Observaciones = payload.Observaciones
                                };

                                var url = $"/api/mobile/v1/rutas/{targetRutaId}/finalizar";
                                var response = await client.PostAsJsonAsync(url, req, cancellationToken);
                                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                                {
                                    response = await client.PostAsJsonAsync($"/api/mobile/v1/rutas/{targetRutaId}/rendicion", req, cancellationToken);
                                }

                                bool ok = await ProcesarRespuestaHttpAsync(op, response, "FinalizarHojaDeRuta", detallesErrores, cancellationToken);
                                if (ok)
                                {
                                    await _localDatabaseService.FinalizarHojaDeRutaLocalAsync(targetRutaId);
                                    exitosas++;
                                }
                                else
                                {
                                    erroresNegocio++;
                                }
                            }
                            break;
                        }

                        case TiposOperacionOffline.RecepcionDeposito:
                        {
                            var payload = JsonSerializer.Deserialize<RecepcionDepositoOfflinePayload>(op.PayloadJson, JsonOptions);
                            if (payload != null)
                            {
                                var req = new
                                {
                                    EnvioId = payload.EnvioId,
                                    CodigosBultosEscaneados = payload.CodigosBultosEscaneados,
                                    CodigosBultosDanados = payload.CodigosBultosDanados,
                                    TipoDiscrepancia = payload.TipoDiscrepancia,
                                    Observaciones = payload.Observaciones
                                };

                                var url = "/api/mobile/v1/deposito/recepcionar";
                                var response = await client.PostAsJsonAsync(url, req, cancellationToken);
                                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                                {
                                    var reqFallback = new
                                    {
                                        CodigosBultosEscaneados = payload.CodigosBultosEscaneados,
                                        CodigosBultosDanados = payload.CodigosBultosDanados,
                                        Observaciones = payload.Observaciones
                                    };
                                    response = await client.PostAsJsonAsync($"/api/deposito/envios/{payload.EnvioId}/recepcion", reqFallback, cancellationToken);
                                }

                                bool ok = await ProcesarRespuestaHttpAsync(op, response, "RecepcionDeposito", detallesErrores, cancellationToken);
                                if (ok)
                                {
                                    await _localDatabaseService.MarcarEnvioAdmitidoRecepcionadoLocalAsync(payload.EnvioId);
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

    private async Task<bool> ProcesarRespuestaHttpAsync(
        OperacionOfflineLocal op,
        HttpResponseMessage response,
        string nombreOp,
        List<string> detallesErrores,
        CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            await _localDatabaseService.MarcarComoSincronizadoAsync(op.Id);
            return true;
        }

        var (tratadoComoExito, _, _) = await ManejarErrorHttpOperacionAsync(
            op,
            response,
            nombreOp,
            ExtraerHojaDeRutaId(op.PayloadJson),
            detallesErrores,
            rutasRendidasEnLote: null,
            ct);

        return tratadoComoExito;
    }

    private async Task<(bool TratadoComoExito, bool RutaTerminada, bool YaEntregado)> ManejarErrorHttpOperacionAsync(
        OperacionOfflineLocal op,
        HttpResponseMessage response,
        string nombreOp,
        Guid? hojaDeRutaId,
        List<string> detallesErrores,
        HashSet<Guid>? rutasRendidasEnLote,
        CancellationToken ct)
    {
        var error = await response.Content.ReadAsStringAsync(ct);
        var status = (int)response.StatusCode;
        var detalle = $"{nombreOp} (Op #{op.Id}): HTTP {status} - {error}";

        bool esDefinitivo = status is 400 or 404 or 409;
        bool mencionaRendidaOCerrada =
            error.Contains("Rendida", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("Cerrada", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("Finalizada", StringComparison.OrdinalIgnoreCase);

        bool mencionaYaEntregado =
            error.Contains("Entregado", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("Entregada", StringComparison.OrdinalIgnoreCase);

        bool mencionaEnTransito =
            error.Contains("EnTransito", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("en tránsito", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("ya se encontraba", StringComparison.OrdinalIgnoreCase);

        if (mencionaRendidaOCerrada)
        {
            var estadoTerminal = error.Contains("Cerrada", StringComparison.OrdinalIgnoreCase) ? "Cerrada" : "Rendida";
            if (hojaDeRutaId.HasValue && hojaDeRutaId.Value != Guid.Empty)
            {
                rutasRendidasEnLote?.Add(hojaDeRutaId.Value);
                await _localDatabaseService.ActualizarEstadoHojaDeRutaAsync(hojaDeRutaId.Value, estadoTerminal);
                await _localDatabaseService.PurgarOperacionesPorRutaAsync(hojaDeRutaId.Value);
            }
            else
            {
                await _localDatabaseService.PurgarOperacionOfflineAsync(op.Id);
            }

            bool silencioso = op.TipoOperacion is TiposOperacionOffline.IniciarRecorrido
                or TiposOperacionOffline.SeleccionarParada
                or TiposOperacionOffline.EventoGeocerca
                or TiposOperacionOffline.TelemetriaGps
                or TiposOperacionOffline.FinalizarHojaDeRuta;

            _logger.LogInformation("Operación {Nombre} (Op #{Id}) purgada por ruta en estado terminal '{Estado}'.", nombreOp, op.Id, estadoTerminal);

            if (!silencioso)
            {
                detallesErrores.Add($"La hoja de ruta ya fue {estadoTerminal.ToLowerInvariant()} en el servidor.");
            }

            return (silencioso, true, false);
        }

        if (esDefinitivo)
        {
            await _localDatabaseService.PurgarOperacionOfflineAsync(op.Id);

            bool esIdempotenteSilencioso =
                (op.TipoOperacion == TiposOperacionOffline.IniciarRecorrido && (mencionaEnTransito || status is 400 or 404 or 409)) ||
                (mencionaYaEntregado && op.TipoOperacion is TiposOperacionOffline.EntregaExitosa or TiposOperacionOffline.EntregaParcial) ||
                op.TipoOperacion is TiposOperacionOffline.TelemetriaGps
                    or TiposOperacionOffline.EventoGeocerca
                    or TiposOperacionOffline.SeleccionarParada
                    or TiposOperacionOffline.FinalizarHojaDeRuta;

            if (esIdempotenteSilencioso)
            {
                _logger.LogInformation("Operación definitiva/idempotente {Nombre} (Op #{Id}) purgada silenciosamente: HTTP {Status}", nombreOp, op.Id, status);
                return (true, false, mencionaYaEntregado);
            }

            _logger.LogWarning("Operación definitiva rechazada y purgada en {Nombre}: {Detalle}", nombreOp, detalle);
            detallesErrores.Add(detalle);
            return (false, false, false);
        }

        _logger.LogWarning("Respuesta no exitosa transitoria en {Nombre}: {Detalle}", nombreOp, detalle);
        await _localDatabaseService.RegistrarErrorAsync(op.Id, detalle);
        detallesErrores.Add(detalle);
        return (false, false, false);
    }

    private static Guid? ExtraerHojaDeRutaId(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("HojaDeRutaId", out var prop) &&
                prop.ValueKind == JsonValueKind.String &&
                Guid.TryParse(prop.GetString(), out var guid) &&
                guid != Guid.Empty)
            {
                return guid;
            }
        }
        catch
        {
            // JSON malformado ignorado
        }

        return null;
    }

    public async Task<List<EnvioAdmitidoMobileDto>> ObtenerEnviosAdmitidosDepositoAsync(CancellationToken cancellationToken = default)
    {
        var resultado = new List<EnvioAdmitidoMobileDto>();

        if (IsOnline)
        {
            try
            {
                if (_themeService != null)
                {
                    await _themeService.SincronizarIdentidadVisualAsync(cancellationToken);
                }

                var client = _httpClientFactory.CreateClient("BackendApi");
                client.BaseAddress = new Uri(ApiEndpoints.BaseUrl);

                var response = await client.GetAsync("/api/mobile/v1/deposito/envios-admitidos", cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    response = await client.GetAsync("/api/deposito/envios-admitidos", cancellationToken);
                }

                if (response.IsSuccessStatusCode)
                {
                    var lista = await response.Content.ReadFromJsonAsync<List<EnvioAdmitidoMobileDto>>(
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                        cancellationToken);

                    if (lista != null)
                    {
                        resultado = lista;

                        // Respaldar en SQLite para soporte offline en depósito
                        var entidadesLocales = lista.Select(e => new EnvioAdmitidoDepositoLocal
                        {
                            EnvioId = e.EnvioId,
                            CodigoSeguimiento = e.CodigoSeguimiento,
                            NombreDestinatario = e.NombreDestinatario,
                            DireccionDestino = e.DireccionDestino,
                            TelefonoDestinatario = e.TelefonoDestinatario,
                            ModalidadServicio = e.ModalidadServicio,
                            PesoTotal = e.PesoTotal,
                            CantidadBultos = e.CantidadBultos,
                            BultosJson = JsonSerializer.Serialize(e.Bultos, JsonOptions),
                            FechaAlta = e.FechaAlta,
                            RecepcionadoLocal = false
                        });

                        await _localDatabaseService.GuardarEnviosAdmitidosDepositoAsync(entidadesLocales);
                        return resultado;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al descargar envíos admitidos desde el servidor. Recurriendo a copia local.");
            }
        }

        // Si no hay red o falló, recuperar de SQLite local
        var locales = await _localDatabaseService.ObtenerEnviosAdmitidosDepositoAsync();
        foreach (var l in locales)
        {
            var bultos = new List<BultoResumenMobileDto>();
            try
            {
                if (!string.IsNullOrWhiteSpace(l.BultosJson))
                {
                    bultos = JsonSerializer.Deserialize<List<BultoResumenMobileDto>>(l.BultosJson, JsonOptions) ?? new();
                }
            }
            catch
            {
                // Fallback bultos vacíos
            }

            resultado.Add(new EnvioAdmitidoMobileDto(
                l.EnvioId,
                l.CodigoSeguimiento,
                l.NombreDestinatario,
                l.DireccionDestino,
                l.TelefonoDestinatario,
                l.ModalidadServicio,
                l.PesoTotal,
                l.CantidadBultos,
                l.FechaAlta,
                bultos
            ));
        }

        return resultado;
    }

    public async Task<bool> RecepcionarBultosDepositoAsync(
        Guid envioId,
        string codigoSeguimiento,
        List<string> codigosEscaneados,
        List<string>? codigosDanados = null,
        string? tipoDiscrepancia = null,
        string? observaciones = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new RecepcionDepositoOfflinePayload
        {
            EnvioId = envioId,
            CodigoSeguimiento = codigoSeguimiento,
            CodigosBultosEscaneados = codigosEscaneados,
            CodigosBultosDanados = codigosDanados,
            TipoDiscrepancia = tipoDiscrepancia,
            Observaciones = observaciones,
            IdempotencyKey = $"RECEP-{envioId:N}-{DateTime.UtcNow.Ticks}",
            FechaRecepcionUtc = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);

        if (IsOnline)
        {
            try
            {
                if (_themeService != null)
                {
                    await _themeService.SincronizarIdentidadVisualAsync(cancellationToken);
                }

                var client = _httpClientFactory.CreateClient("BackendApi");
                client.BaseAddress = new Uri(ApiEndpoints.BaseUrl);

                var req = new
                {
                    EnvioId = envioId,
                    CodigosBultosEscaneados = codigosEscaneados,
                    CodigosBultosDanados = codigosDanados,
                    TipoDiscrepancia = tipoDiscrepancia,
                    Observaciones = observaciones
                };

                var response = await client.PostAsJsonAsync("/api/mobile/v1/deposito/recepcionar", req, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var reqFallback = new
                    {
                        CodigosBultosEscaneados = codigosEscaneados,
                        CodigosBultosDanados = codigosDanados,
                        Observaciones = observaciones
                    };
                    response = await client.PostAsJsonAsync($"/api/deposito/envios/{envioId}/recepcion", reqFallback, cancellationToken);
                }

                if (response.IsSuccessStatusCode)
                {
                    await _localDatabaseService.MarcarEnvioAdmitidoRecepcionadoLocalAsync(envioId);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fallo al enviar recepción al servidor. Encolando para sincronización posterior.");
            }
        }

        // Modo offline o fallo transitorio de red: encolar localmente
        await _localDatabaseService.MarcarEnvioAdmitidoRecepcionadoLocalAsync(envioId);
        await _localDatabaseService.EncolarOperacionAsync(TiposOperacionOffline.RecepcionDeposito, json);
        await NotificarCambioEstadoAsync("Recepción registrada localmente en depósito. Pendiente de sincronización.");
        return true;
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
