using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.Services.Tracking;
using UltimaMilla.Mobile.Views.Ruta;

namespace UltimaMilla.Mobile.ViewModels.Ruta;

public class BultoItemViewModel
{
    public string Codigo { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public decimal PesoKg { get; init; }
    public bool EsFragil { get; init; }
    public string Icono => EsFragil ? "FRÁGIL" : "ESTÁNDAR";
}

public partial class ParadaItemViewModel : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid HojaDeRutaId { get; set; } = Guid.Empty;
    public Guid EnvioId { get; init; } = Guid.Empty;
    public int Orden { get; init; }
    public string Direccion { get; init; } = string.Empty;
    public string Ciudad { get; init; } = string.Empty;
    public string Destinatario { get; init; } = string.Empty;
    public string Telefono { get; init; } = string.Empty;
    public int CantidadBultos { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstadoBgColor))]
    [NotifyPropertyChangedFor(nameof(EstadoTextColor))]
    [NotifyPropertyChangedFor(nameof(EstadoBorderColor))]
    [NotifyPropertyChangedFor(nameof(EstadoTextoBadge))]
    [NotifyPropertyChangedFor(nameof(PuedeRegistrar))]
    private string _estado = "Pendiente"; // "Pendiente", "Entregado", "Fallido", "EnDevolucion"

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarBadgeGeocerca))]
    [NotifyPropertyChangedFor(nameof(GeocercaTextoBadge))]
    [NotifyPropertyChangedFor(nameof(GeocercaBgColor))]
    [NotifyPropertyChangedFor(nameof(GeocercaBorderColor))]
    [NotifyPropertyChangedFor(nameof(GeocercaTextColor))]
    private bool _enGeocerca;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarDistanciaGeocerca))]
    [NotifyPropertyChangedFor(nameof(DistanciaFormateadaTexto))]
    [NotifyPropertyChangedFor(nameof(GeocercaTextoBadge))]
    private double? _distanciaActualMetros;

    public string HoraEstimada { get; init; } = string.Empty;
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string NotasComercio { get; init; } = string.Empty;
    public double Latitud { get; init; }
    public double Longitud { get; init; }
    public List<BultoItemViewModel> Bultos { get; init; } = new();

    // Helpers visuales para el renderizado del badge de estado en XAML
    public Color EstadoBgColor => Estado switch
    {
        "Entregado" => Color.FromArgb("#DCFCE7"),
        "Fallido" or "EnDevolucion" => Color.FromArgb("#FEE2E2"),
        _ => Color.FromArgb("#FEF3C7")
    };

    public Color EstadoTextColor => Estado switch
    {
        "Entregado" => Color.FromArgb("#15803D"),
        "Fallido" or "EnDevolucion" => Color.FromArgb("#B91C1C"),
        _ => Color.FromArgb("#B45309")
    };

    public Color EstadoBorderColor => Estado switch
    {
        "Entregado" => Color.FromArgb("#86EFAC"),
        "Fallido" or "EnDevolucion" => Color.FromArgb("#FCA5A5"),
        _ => Color.FromArgb("#FCD34D")
    };

    public string EstadoTextoBadge => Estado switch
    {
        "Entregado" => "Entregado",
        "EnDevolucion" => "En Devolución",
        "Fallido" => "Fallido",
        _ => "Pendiente"
    };

    // Helpers visuales para Geocercas
    public bool MostrarBadgeGeocerca => EnGeocerca;

    public bool MostrarDistanciaGeocerca => DistanciaActualMetros.HasValue;

    public string GeocercaTextoBadge => EnGeocerca
        ? (DistanciaActualMetros.HasValue
            ? $"En Geocerca (<100m - {DistanciaActualMetros.Value:F0}m)"
            : "En Geocerca (<100m)")
        : string.Empty;

    public string DistanciaFormateadaTexto => DistanciaActualMetros.HasValue
        ? (DistanciaActualMetros.Value >= 1000.0
            ? $"Distancia GPS: {(DistanciaActualMetros.Value / 1000.0):F2} km"
            : $"Distancia GPS: {DistanciaActualMetros.Value:F0} m")
        : string.Empty;

    public Color GeocercaBgColor => Color.FromArgb("#DBEAFE");
    public Color GeocercaBorderColor => Color.FromArgb("#93C5FD");
    public Color GeocercaTextColor => Color.FromArgb("#1E40AF");

    public bool PuedeRegistrar => Estado == "Pendiente";
}

public partial class RutaDiaViewModel : ObservableObject, IDisposable
{
    private readonly ILocalDatabaseService? _localDatabaseService;
    private readonly ISyncService? _syncService;
    private readonly IGeolocalizacionTrackingService? _trackingService;
    private readonly IGeofenceService? _geofenceService;

    [ObservableProperty]
    private string _fechaHojaRuta;

    [ObservableProperty]
    private bool _isOffline = true;

    [ObservableProperty]
    private bool _isOfflineSimulado = false;

    [ObservableProperty]
    private double _progresoRuta = 0.0;

    [ObservableProperty]
    private string _resumenProgresoTexto = "0 de 0 paradas completadas";

    [ObservableProperty]
    private string _progresoPorcentajeTexto = "0%";

    [ObservableProperty]
    private int _colaOfflineCount = 0;

    [ObservableProperty]
    private string _textoBadgeConectividad = "Modo Offline";

    [ObservableProperty]
    private Color _badgeConectividadBgColor = Color.FromArgb("#FEF2F2");

    [ObservableProperty]
    private Color _badgeConectividadBorderColor = Color.FromArgb("#FCA5A5");

    [ObservableProperty]
    private Color _badgeConectividadTextColor = Color.FromArgb("#991B1B");

    [ObservableProperty]
    private Color _indicadorConexionDotColor = Color.FromArgb("#DC2626");

    // =========================================================================
    // TELEMETRÍA, GEOCERCAS Y BATERÍA ADAPTATIVA
    // =========================================================================
    [ObservableProperty]
    private bool _isTrackingGpsActivo = false;

    [ObservableProperty]
    private string _estadoTrackingTexto = "GPS en vivo";

    [ObservableProperty]
    private Color _trackingBadgeBgColor = Color.FromArgb("#ECFDF5");

    [ObservableProperty]
    private Color _trackingBadgeBorderColor = Color.FromArgb("#A7F3D0");

    [ObservableProperty]
    private Color _trackingBadgeTextColor = Color.FromArgb("#065F46");

    [ObservableProperty]
    private Color _trackingDotColor = Color.FromArgb("#10B981");

    [ObservableProperty]
    private string _ultimaUbicacionTexto = "Sin coordenadas recientes";

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<ParadaItemViewModel> Paradas { get; } = new();

    public RutaDiaViewModel() : this(null, null, null, null)
    {
    }

    public RutaDiaViewModel(
        ILocalDatabaseService? localDatabaseService,
        ISyncService? syncService,
        IGeolocalizacionTrackingService? trackingService,
        IGeofenceService? geofenceService = null)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;
        _trackingService = trackingService;
        _geofenceService = geofenceService;
        _fechaHojaRuta = DateTime.Now.ToString("dd/MM/yyyy");

        if (_syncService != null)
        {
            _syncService.OnSyncStateChanged += OnSyncStateChangedHandler;
        }

        if (_trackingService != null)
        {
            _trackingService.OnTrackingStateChanged += OnTrackingStateChangedHandler;
            _trackingService.OnPosicionActualizada += OnPosicionActualizadaHandler;
            ActualizarEstadoVisualTracking(_trackingService.IsTrackingActive);
        }

        if (_geofenceService != null)
        {
            _geofenceService.OnGeocercasEvaluadas += OnGeocercasEvaluadasHandler;
        }

        ActualizarEstadoVisualConectividad(_syncService?.IsOnline ?? false, 0);
    }

    private void OnSyncStateChangedHandler(object? sender, SyncStateChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            ActualizarEstadoVisualConectividad(e.IsOnline, e.OperacionesPendientes);
            await RefrescarParadasDesdeSQLiteAsync();
        });
    }

    private void OnTrackingStateChangedHandler(object? sender, bool activo)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsTrackingGpsActivo = activo;
            ActualizarEstadoVisualTracking(activo);
        });
    }

    private void OnPosicionActualizadaHandler(object? sender, PosicionActualizadaEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UltimaUbicacionTexto = $"{e.Latitud:F4}, {e.Longitud:F4} ({e.Timestamp.ToLocalTime():HH:mm:ss})";
            if (e.TransmitidaSignalR)
            {
                EstadoTrackingTexto = "GPS en vivo";
                TrackingDotColor = Color.FromArgb("#16A34A");
            }
            else
            {
                EstadoTrackingTexto = "GPS en vivo";
                TrackingDotColor = Color.FromArgb("#10B981");
            }
        });
    }

    private void OnGeocercasEvaluadasHandler(object? sender, EvaluacionGeocercasResult resultado)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            AplicarEstadosGeocercaEnParadas(resultado);
        });
    }

    private void AplicarEstadosGeocercaEnParadas(EvaluacionGeocercasResult? resultado = null)
    {
        foreach (var parada in Paradas)
        {
            if (resultado != null)
            {
                var estado = resultado.EstadosPorParada.FirstOrDefault(e =>
                    e.ParadaId == parada.Id
                    || (!string.IsNullOrWhiteSpace(e.CodigoSeguimiento) && e.CodigoSeguimiento == parada.CodigoSeguimiento)
                    || e.Orden == parada.Orden);

                if (estado != null)
                {
                    parada.DistanciaActualMetros = estado.DistanciaMetros;
                    parada.EnGeocerca = estado.DentroDeGeocerca;
                    continue;
                }
            }

            if (_geofenceService != null)
            {
                parada.EnGeocerca = _geofenceService.EstaEnGeocerca(parada.Id);
                parada.DistanciaActualMetros = _geofenceService.ObtenerUltimaDistanciaMetros(parada.Id);
            }
        }
    }

    private void ActualizarEstadoVisualTracking(bool activo)
    {
        if (activo)
        {
            EstadoTrackingTexto = "GPS en vivo";
            TrackingBadgeBgColor = Color.FromArgb("#ECFDF5");
            TrackingBadgeBorderColor = Color.FromArgb("#A7F3D0");
            TrackingBadgeTextColor = Color.FromArgb("#065F46");
            TrackingDotColor = Color.FromArgb("#10B981");
        }
        else
        {
            EstadoTrackingTexto = "GPS inactivo";
            TrackingBadgeBgColor = Color.FromArgb("#F3F4F6");
            TrackingBadgeBorderColor = Color.FromArgb("#E5E7EB");
            TrackingBadgeTextColor = Color.FromArgb("#4B5563");
            TrackingDotColor = Color.FromArgb("#9CA3AF");
        }
    }

    public async Task InicializarAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;

            if (_syncService is not null)
            {
                var paradas = await _syncService.DescargarRutaDelDiaAsync();
                Paradas.Clear();
                foreach (var p in paradas)
                {
                    Paradas.Add(p);
                }

                int pendientes = await _syncService.ObtenerCantidadPendientesAsync();
                ActualizarEstadoVisualConectividad(_syncService.IsOnline, pendientes);
            }
            else if (_localDatabaseService is not null)
            {
                var local = await _localDatabaseService.ObtenerRutaActivaLocalAsync();
                Paradas.Clear();
                if (local != null)
                {
                    foreach (var p in local.Paradas)
                    {
                        Paradas.Add(p);
                    }
                }
                int pendientes = await _localDatabaseService.ObtenerCantidadPendientesAsync();
                ActualizarEstadoVisualConectividad(false, pendientes);
            }

            AplicarEstadosGeocercaEnParadas();
            RecalcularProgreso();
            await EvaluarYActualizarTrackingAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefrescarParadasDesdeSQLiteAsync()
    {
        if (_localDatabaseService is null)
            return;

        try
        {
            var local = await _localDatabaseService.ObtenerRutaActivaLocalAsync();
            if (local != null && local.Paradas.Count > 0)
            {
                Paradas.Clear();
                foreach (var p in local.Paradas)
                {
                    Paradas.Add(p);
                }
                AplicarEstadosGeocercaEnParadas();
            }
            RecalcularProgreso();
            await EvaluarYActualizarTrackingAsync();
        }
        catch
        {
            // Fallback
        }
    }

    public async Task EvaluarYActualizarTrackingAsync()
    {
        if (_trackingService == null)
            return;

        bool hayParadasPendientes = Paradas.Any(p => p.Estado == "Pendiente");

        if (hayParadasPendientes)
        {
            var rutaLocal = _localDatabaseService != null ? await _localDatabaseService.ObtenerRutaActivaLocalAsync() : null;
            var rutaId = rutaLocal?.Ruta?.Id ?? (Paradas.FirstOrDefault()?.HojaDeRutaId ?? DemoDataDefaults.DefaultHojaDeRutaId);
            if (rutaId == Guid.Empty)
                rutaId = DemoDataDefaults.DefaultHojaDeRutaId;

            var repartidorId = DemoDataDefaults.DefaultRepartidorId;

            if (!_trackingService.IsTrackingActive)
            {
                await _trackingService.IniciarTrackingAsync(rutaId, repartidorId);
            }
        }
        else if (Paradas.Count > 0 && !hayParadasPendientes)
        {
            // Se completaron todas las entregas de la jornada: detener tracking automáticamente
            if (_trackingService.IsTrackingActive)
            {
                await _trackingService.DetenerTrackingAsync();
            }
        }
    }

    public void ActualizarEstadoVisualConectividad(bool isOnline, int pendientes)
    {
        IsOffline = !isOnline;
        IsOfflineSimulado = _syncService?.IsOfflineSimulado ?? false;
        ColaOfflineCount = pendientes;

        if (isOnline)
        {
            TextoBadgeConectividad = pendientes > 0 ? $"En Línea ({pendientes} pend.)" : "En Línea";
            BadgeConectividadBgColor = Color.FromArgb("#F0FDF4");
            BadgeConectividadBorderColor = Color.FromArgb("#86EFAC");
            BadgeConectividadTextColor = Color.FromArgb("#166534");
            IndicadorConexionDotColor = Color.FromArgb("#16A34A");
        }
        else
        {
            string motivo = IsOfflineSimulado ? "Offline (Demo)" : "Modo Offline";
            TextoBadgeConectividad = pendientes > 0 ? $"{motivo} ({pendientes} pend.)" : motivo;
            BadgeConectividadBgColor = Color.FromArgb("#FEF2F2");
            BadgeConectividadBorderColor = Color.FromArgb("#FCA5A5");
            BadgeConectividadTextColor = Color.FromArgb("#991B1B");
            IndicadorConexionDotColor = Color.FromArgb("#DC2626");
        }
    }

    public void RecalcularProgreso()
    {
        if (Paradas.Count == 0)
        {
            ProgresoRuta = 0;
            ResumenProgresoTexto = "0 de 0 paradas completadas";
            ProgresoPorcentajeTexto = "0%";
            return;
        }

        int completadas = Paradas.Count(p => p.Estado == "Entregado" || p.Estado == "Fallido" || p.Estado == "EnDevolucion");
        int total = Paradas.Count;
        ProgresoRuta = (double)completadas / total;
        ResumenProgresoTexto = $"{completadas} de {total} paradas completadas";
        ProgresoPorcentajeTexto = $"{Math.Round(ProgresoRuta * 100)}%";

        if (total > 0 && completadas == total && _trackingService is { IsTrackingActive: true })
        {
            _ = Task.Run(async () => await _trackingService.DetenerTrackingAsync());
        }
    }

    [RelayCommand]
    private async Task MostrarColaOfflineAsync()
    {
        if (Shell.Current is null)
            return;

        int pendientes = _syncService != null
            ? await _syncService.ObtenerCantidadPendientesAsync()
            : (_localDatabaseService != null ? await _localDatabaseService.ObtenerCantidadPendientesAsync() : 0);

        ColaOfflineCount = pendientes;

        string opcionToggle = (_syncService?.IsOfflineSimulado ?? false)
            ? "Desactivar Modo Offline (Pasar a Online)"
            : "Activar Modo Offline Simulado (Demo)";

        const string opcionSync = "Sincronizar Ahora con Backend";
        const string opcionReset = "Reiniciar Datos de Demo (Local + Backend)";

        var seleccion = await Shell.Current.DisplayActionSheetAsync(
            $"Conectividad y Cola Offline ({pendientes} pendientes)",
            "Cerrar",
            null,
            opcionToggle,
            opcionSync,
            opcionReset);

        if (seleccion == opcionToggle && _syncService is not null)
        {
            await _syncService.AlternarModoOfflineSimuladoAsync();

            if (_syncService.IsOnline)
            {
                var syncRes = await _syncService.SincronizarOperacionesPendientesAsync();
                int pendientesRestantes = await _syncService.ObtenerCantidadPendientesAsync();
                ActualizarEstadoVisualConectividad(true, pendientesRestantes);
                await RefrescarParadasDesdeSQLiteAsync();

                string mensaje = $"Modo Online activado.\n{syncRes.Mensaje}";
                await Shell.Current.DisplayAlertAsync("Sincronización al Volver a Línea", mensaje, "Aceptar");
            }
            else
            {
                int p = await _syncService.ObtenerCantidadPendientesAsync();
                ActualizarEstadoVisualConectividad(false, p);
                await Shell.Current.DisplayAlertAsync(
                    "Modo de Conexión",
                    "Modo Offline Simulado activado para demostración. Las operaciones se registrarán de forma 100% local en SQLite cifrado.",
                    "Aceptar");
            }

            await RefrescarParadasDesdeSQLiteAsync();
        }
        else if (seleccion == opcionSync && _syncService is not null)
        {
            if (!_syncService.IsOnline)
            {
                await Shell.Current.DisplayAlertAsync(
                    "No es posible sincronizar",
                    "El dispositivo se encuentra en Modo Offline. Desactive el modo offline simulado para transferir la cola al backend.",
                    "Entendido");
                return;
            }

            var resultado = await _syncService.SincronizarOperacionesPendientesAsync();
            int pendientesRestantes = await _syncService.ObtenerCantidadPendientesAsync();
            ActualizarEstadoVisualConectividad(true, pendientesRestantes);
            await Shell.Current.DisplayAlertAsync("Sincronización de Cola", resultado.Mensaje, "Aceptar");
            await RefrescarParadasDesdeSQLiteAsync();
        }
        else if (seleccion == opcionReset && _syncService is not null)
        {
            bool confirmar = await Shell.Current.DisplayAlertAsync(
                "Reiniciar Datos de Demo",
                "¿Desea restablecer los envíos ENV-2026-00108, ENV-2026-00109 y ENV-2026-00110 a estado 'EnTransito' (4) en PostgreSQL y limpiar SQLite local (cola en 0)?",
                "Sí, reiniciar demo",
                "Cancelar");

            if (confirmar)
            {
                IsBusy = true;
                try
                {
                    _geofenceService?.ReiniciarEstadosMemoria();
                    var res = await _syncService.ReiniciarDemoCompletaAsync();
                    ActualizarEstadoVisualConectividad(_syncService.IsOnline, 0);
                    await RefrescarParadasDesdeSQLiteAsync();
                    await Shell.Current.DisplayAlertAsync("Reinicio de Demo", res, "Aceptar");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
    }

    [RelayCommand]
    private async Task IrAParadaAsync(ParadaItemViewModel? parada)
    {
        if (parada is null)
            return;

        await Shell.Current.GoToAsync("DetalleParadaPage", new Dictionary<string, object>
        {
            { "Parada", parada }
        });
    }

    [RelayCommand]
    private async Task NavegarAParadaAsync(ParadaItemViewModel? parada)
    {
        if (parada is null)
            return;

        try
        {
            var destination = (parada.Latitud != 0 && parada.Longitud != 0)
                ? $"{parada.Latitud.ToString(System.Globalization.CultureInfo.InvariantCulture)},{parada.Longitud.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : Uri.EscapeDataString($"{parada.Direccion}, {parada.Ciudad}");

            if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                var mapsUrl = $"https://www.google.com/maps/dir/?api=1&destination={destination}";
                await Launcher.Default.OpenAsync(mapsUrl);
            }
            else
            {
                var location = new Location(parada.Latitud, parada.Longitud);
                var options = new MapLaunchOptions
                {
                    Name = parada.Direccion,
                    NavigationMode = NavigationMode.Driving
                };

                await Map.Default.OpenAsync(location, options);
            }
        }
        catch
        {
            var fallbackDest = Uri.EscapeDataString($"{parada.Direccion}, {parada.Ciudad}");
            await Launcher.Default.OpenAsync($"https://www.google.com/maps/dir/?api=1&destination={fallbackDest}");
        }
    }

    [RelayCommand]
    private async Task AlternarTrackingGpsAsync()
    {
        await GestionarGeocercasYBateriaAsync();
    }

    [RelayCommand]
    private async Task GestionarGeocercasYBateriaAsync()
    {
        if (Shell.Current is null || _trackingService is null)
            return;

        const string opcionEntrada = "Simular Entrada a Geocerca - Parada 1 (45m)";
        const string opcionSalida = "Simular Salida de Geocerca - Parada 1 (220m)";
        const string opcionAnalisis = "Ver Análisis de Batería e Historial de Geocercas";
        string opcionToggleGps = _trackingService.IsTrackingActive
            ? "Pausar Rastreo GPS Automático"
            : "Activar Rastreo GPS Automático";

        var seleccion = await Shell.Current.DisplayActionSheetAsync(
            "Geocercas y Muestreo Adaptativo de Batería",
            "Cerrar",
            null,
            opcionEntrada,
            opcionSalida,
            opcionAnalisis,
            opcionToggleGps);

        if (seleccion == opcionEntrada)
        {
            var evaluacion = await _trackingService.SimularPosicionGpsAsync(
                DemoDataDefaults.Parada1SimulacionEntrada45mLat,
                DemoDataDefaults.Parada1SimulacionEntrada45mLon);

            AplicarEstadosGeocercaEnParadas(evaluacion);
            ActualizarEstadoVisualTracking(true);

            int pendientes = _syncService != null ? await _syncService.ObtenerCantidadPendientesAsync() : 0;
            ActualizarEstadoVisualConectividad(_syncService?.IsOnline ?? false, pendientes);

            var estadoParada1 = evaluacion.EstadosPorParada.FirstOrDefault(p => p.Orden == 1 || p.CodigoSeguimiento == "ENV-2026-00108");
            double distanciaCalculada = estadoParada1?.DistanciaMetros ?? 45.0;
            bool disparoNuevo = evaluacion.EventosDisparados.Any(e => e.TipoEvento == "LLEGADA_GEOCERCA");
            string estadoSync = (_syncService?.IsOnline ?? false)
                ? "Sincronizado con el backend (POST /geocerca)"
                : "Guardado en SQLite local y encolado en OperacionOfflineLocal (Modo Offline)";

            string detalleEvento = disparoNuevo
                ? "Evento disparado: LLEGADA_GEOCERCA (Radio de entrada < 100m)"
                : "Histéresis activa: El repartidor ya se encontraba dentro de la geocerca (< 100m), no se duplicó el evento de entrada.";

            await Shell.Current.DisplayAlertAsync(
                "Geocerca Detectada - Parada 1",
                $"Posición GPS inyectada frente a Av. 18 de Julio 1450 (ENV-2026-00108).\n\n" +
                $"- Distancia Haversine calculada: {distanciaCalculada:F1} metros\n" +
                $"- Estado de Parada 1: En Geocerca (<100m)\n" +
                $"- {detalleEvento}\n" +
                $"- Perfil de muestreo: {_trackingService.PerfilAdaptativoActual}\n" +
                $"- Persistencia: {estadoSync}",
                "Aceptar");
        }
        else if (seleccion == opcionSalida)
        {
            var evaluacion = await _trackingService.SimularPosicionGpsAsync(
                DemoDataDefaults.Parada1SimulacionSalida220mLat,
                DemoDataDefaults.Parada1SimulacionSalida220mLon);

            AplicarEstadosGeocercaEnParadas(evaluacion);

            int pendientes = _syncService != null ? await _syncService.ObtenerCantidadPendientesAsync() : 0;
            ActualizarEstadoVisualConectividad(_syncService?.IsOnline ?? false, pendientes);

            var estadoParada1 = evaluacion.EstadosPorParada.FirstOrDefault(p => p.Orden == 1 || p.CodigoSeguimiento == "ENV-2026-00108");
            double distanciaCalculada = estadoParada1?.DistanciaMetros ?? 220.0;
            var eventoSalida = evaluacion.EventosDisparados.FirstOrDefault(e => e.TipoEvento == "SALIDA_GEOCERCA");
            int permanenciaSeg = eventoSalida?.TiempoPermanenciaSegundos ?? estadoParada1?.UltimoTiempoPermanenciaSegundos ?? 0;

            string estadoSync = (_syncService?.IsOnline ?? false)
                ? "Sincronizado con el backend (POST /geocerca)"
                : "Guardado en SQLite local y encolado en OperacionOfflineLocal (Modo Offline)";

            string detalleSalida = eventoSalida != null
                ? $"Evento disparado: SALIDA_GEOCERCA (Radio de salida > 150m)\n- Tiempo de permanencia registrado: {permanenciaSeg} segundos"
                : "Nota de histéresis: El repartidor no tenía una entrada activa previa en Parada 1. Simule primero la entrada (45m) y luego la salida (220m).";

            await Shell.Current.DisplayAlertAsync(
                "Salida de Geocerca - Parada 1",
                $"Posición GPS inyectada a las afueras de Av. 18 de Julio 1450 (ENV-2026-00108).\n\n" +
                $"- Distancia Haversine calculada: {distanciaCalculada:F1} metros\n" +
                $"- {detalleSalida}\n" +
                $"- Perfil de muestreo: {_trackingService.PerfilAdaptativoActual}\n" +
                $"- Persistencia: {estadoSync}",
                "Aceptar");
        }
        else if (seleccion == opcionAnalisis)
        {
            var informe = await _trackingService.ObtenerInformeBateriaEHistorialGeocercasAsync();
            await Shell.Current.DisplayAlertAsync(
                "Análisis de Batería e Historial de Geocercas",
                informe,
                "Cerrar");
        }
        else if (seleccion == opcionToggleGps)
        {
            if (_trackingService.IsTrackingActive)
            {
                await _trackingService.DetenerTrackingAsync();
                await Shell.Current.DisplayAlertAsync(
                    "Tracking GPS",
                    "Rastreo de ubicación en tiempo real pausado.",
                    "Aceptar");
            }
            else
            {
                var rutaId = Paradas.FirstOrDefault()?.HojaDeRutaId ?? DemoDataDefaults.DefaultHojaDeRutaId;
                if (rutaId == Guid.Empty)
                    rutaId = DemoDataDefaults.DefaultHojaDeRutaId;

                await _trackingService.IniciarTrackingAsync(rutaId, DemoDataDefaults.DefaultRepartidorId);
                await Shell.Current.DisplayAlertAsync(
                    "Tracking GPS en Vivo",
                    $"Rastreo adaptativo iniciado.\nPerfil actual: {_trackingService.PerfilAdaptativoActual}\nIntervalo: {_trackingService.IntervaloTracking.TotalSeconds}s.",
                    "Entendido");
            }
        }
    }

    public void Dispose()
    {
        if (_syncService != null)
        {
            _syncService.OnSyncStateChanged -= OnSyncStateChangedHandler;
        }

        if (_trackingService != null)
        {
            _trackingService.OnTrackingStateChanged -= OnTrackingStateChangedHandler;
            _trackingService.OnPosicionActualizada -= OnPosicionActualizadaHandler;
        }

        if (_geofenceService != null)
        {
            _geofenceService.OnGeocercasEvaluadas -= OnGeocercasEvaluadasHandler;
        }

        GC.SuppressFinalize(this);
    }
}
