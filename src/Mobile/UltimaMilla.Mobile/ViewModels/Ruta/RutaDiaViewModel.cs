using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.Local;
using UltimaMilla.Mobile.Services.Auth;
using UltimaMilla.Mobile.Services.Notifications;
using UltimaMilla.Mobile.Services.Optimization;
using UltimaMilla.Mobile.Services.Realtime;
using UltimaMilla.Mobile.Services.Security;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.Services.Theme;
using UltimaMilla.Mobile.Services.Tracking;
using UltimaMilla.Mobile.Views.Ruta;

namespace UltimaMilla.Mobile.ViewModels.Ruta;

public class EventoGeocercaItemViewModel
{
    public string HoraTexto { get; set; } = string.Empty;
    public string CodigoSeguimiento { get; set; } = string.Empty;
    public string TipoEventoTexto { get; set; } = string.Empty;
    public Color TipoEventoBadgeBg { get; set; } = Color.FromArgb("#F1F5F9");
    public Color TipoEventoBadgeTextColor { get; set; } = Color.FromArgb("#334155");
    public string PermanenciaTexto { get; set; } = string.Empty;
    public bool TienePermanencia { get; set; }
}

public partial class BultoItemViewModel : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ParadaId { get; init; } = Guid.Empty;
    public Guid EnvioId { get; init; } = Guid.Empty;
    public string Codigo { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public decimal PesoKg { get; init; }
    public bool EsFragil { get; init; }
    public string Icono => EsFragil ? "FRÁGIL" : "ESTÁNDAR";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstadoCargaTexto))]
    [NotifyPropertyChangedFor(nameof(BadgeCargaBgColor))]
    [NotifyPropertyChangedFor(nameof(BadgeCargaTextColor))]
    private bool _verificadoEnCarga;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstadoEntregaTexto))]
    [NotifyPropertyChangedFor(nameof(BadgeEntregaBgColor))]
    [NotifyPropertyChangedFor(nameof(BadgeEntregaTextColor))]
    [NotifyPropertyChangedFor(nameof(FondoCardColor))]
    private bool _verificadoEnEntrega;

    public string EstadoCargaTexto => VerificadoEnCarga ? "Cargado y Verificado" : "Pendiente de Escaneo";
    public Color BadgeCargaBgColor => VerificadoEnCarga ? Color.FromArgb("#F0FDF4") : Color.FromArgb("#F1F5F9");
    public Color BadgeCargaTextColor => VerificadoEnCarga ? Color.FromArgb("#16A34A") : Color.FromArgb("#64748B");

    public string EstadoEntregaTexto => VerificadoEnEntrega ? "Verificado para entrega" : "Pendiente de verificación";
    public Color BadgeEntregaBgColor => VerificadoEnEntrega ? Color.FromArgb("#F0FDF4") : Color.FromArgb("#F1F5F9");
    public Color BadgeEntregaTextColor => VerificadoEnEntrega ? Color.FromArgb("#16A34A") : Color.FromArgb("#64748B");
    public Color FondoCardColor => VerificadoEnEntrega ? Color.FromArgb("#F0FDF4") : Color.FromArgb("#F8FAFC");
}

public partial class ParadaItemViewModel : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid HojaDeRutaId { get; set; } = Guid.Empty;
    public Guid EnvioId { get; init; } = Guid.Empty;

    /// <summary>"Entrega" o "Retiro" (retiro en domicilio).</summary>
    public string Tipo { get; init; } = "Entrega";
    public Guid? RetiroId { get; init; }
    public bool EsRetiro => string.Equals(Tipo, "Retiro", StringComparison.OrdinalIgnoreCase);
    public string TipoTexto => EsRetiro ? "RETIRO" : "ENTREGA";

    [ObservableProperty]
    private int _orden;

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

    [ObservableProperty]
    private string _horaEstimada = string.Empty;

    public string VentanaHoraria { get; init; } = "09:00 - 13:00";
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string NotasComercio { get; init; } = string.Empty;
    public double Latitud { get; init; }
    public double Longitud { get; init; }
    public List<BultoItemViewModel> Bultos { get; init; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstadoBgColor))]
    [NotifyPropertyChangedFor(nameof(EstadoTextColor))]
    [NotifyPropertyChangedFor(nameof(EstadoBorderColor))]
    [NotifyPropertyChangedFor(nameof(EstadoTextoBadge))]
    [NotifyPropertyChangedFor(nameof(PuedeRegistrar))]
    private bool _enPrecarga;

    // Helpers visuales para el renderizado del badge de estado en XAML
    public Color EstadoBgColor => Estado switch
    {
        "Entregado" => Color.FromArgb("#DCFCE7"),
        "Fallido" or "EnDevolucion" => Color.FromArgb("#FEE2E2"),
        _ => EnPrecarga ? Color.FromArgb("#F1F5F9") : Color.FromArgb("#FEF3C7")
    };

    public Color EstadoTextColor => Estado switch
    {
        "Entregado" => Color.FromArgb("#15803D"),
        "Fallido" or "EnDevolucion" => Color.FromArgb("#B91C1C"),
        _ => EnPrecarga ? Color.FromArgb("#475569") : Color.FromArgb("#B45309")
    };

    public Color EstadoBorderColor => Estado switch
    {
        "Entregado" => Color.FromArgb("#86EFAC"),
        "Fallido" or "EnDevolucion" => Color.FromArgb("#FCA5A5"),
        _ => EnPrecarga ? Color.FromArgb("#CBD5E1") : Color.FromArgb("#FCD34D")
    };

    public string EstadoTextoBadge => Estado switch
    {
        "Entregado" => EsRetiro ? "Retirado" : "Entregado",
        "EnDevolucion" => "En Devolución",
        "Fallido" => "Fallido",
        _ => EnPrecarga ? "Pendiente de Carga en Depósito" : "Pendiente"
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

    public bool PuedeRegistrar => Estado == "Pendiente" && !EnPrecarga;
}

public partial class RutaDiaViewModel : ObservableObject, IDisposable
{
    private readonly ILocalDatabaseService? _localDatabaseService;
    private readonly ISyncService? _syncService;
    private readonly IGeolocalizacionTrackingService? _trackingService;
    private readonly IGeofenceService? _geofenceService;
    private readonly IAuthService? _authService;
    private readonly IRutaOptimizationService _optimizationService;
    private readonly IBiometricAuthService? _biometricAuthService;
    private readonly IOperadorThemeService? _themeService;
    private readonly INotificacionLocalService? _notificacionService;
    private readonly IRealtimeTrackingService? _realtimeTrackingService;

    [ObservableProperty]
    private int _totalParadasCount;

    [ObservableProperty]
    private int _paradasPendientesCount;

    [ObservableProperty]
    private int _paradasCompletadasCount;

    [ObservableProperty]
    private string _fechaHojaRuta;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecorridoNoIniciado))]
    private string _estadoHojaDeRuta = "EnTransito";

    public bool RecorridoNoIniciado => EstadoHojaDeRuta is "Liberada" or "EnCarga" or "Borrador";

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
    private bool _foregroundServiceActivo = false;

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
    private string _ultimaCoordenadaTexto = "Sin señal GPS";

    [ObservableProperty]
    private string _ultimoEstadoServidorTexto = "Servidor: Pendiente";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayRutaAsignada))]
    private bool _sinRutaAsignada;

    public bool HayRutaAsignada => !SinRutaAsignada;

    [ObservableProperty]
    private bool _puedeFinalizarRuta;

    [ObservableProperty]
    private bool _tieneMultiplesRutas;

    [ObservableProperty]
    private HojaDeRutaSelectorItem? _hojaDeRutaSeleccionada;
    private bool _actualizandoSelector;
    private readonly SemaphoreSlim _uiRefreshLock = new(1, 1);

    public ObservableCollection<HojaDeRutaSelectorItem> HojasDeRutaDisponibles { get; } = new();

    async partial void OnHojaDeRutaSeleccionadaChanged(HojaDeRutaSelectorItem? value)
    {
        if (_actualizandoSelector || value == null || IsBusy)
            return;

        try
        {
            await CargarRutaEspecificaAsync(value.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RutaDiaViewModel] Error cambiando hoja de ruta: {ex.Message}");
        }
    }

    // =========================================================================
    // OPTIMIZACIÓN DEL ORDEN DE PARADAS (VECINO MÁS CERCANO + 2-OPT)
    // =========================================================================
    [ObservableProperty]
    private bool _mostrarResumenOptimizacion;

    [ObservableProperty]
    private string _resumenCriterioEstandarTexto = string.Empty;

    [ObservableProperty]
    private string _resumenRutaOptimizadaTexto = string.Empty;

    [ObservableProperty]
    private string _resumenAhorroRecorridoTexto = string.Empty;

    [ObservableProperty]
    private string _resumenTiempoReducidoTexto = string.Empty;

    [ObservableProperty]
    private string _resumenSecuenciaAplicadaTexto = string.Empty;

    // =========================================================================
    // MENÚ DESPLEGABLE DE HERRAMIENTAS Y MODALES CORPORATIVOS EN XAML
    // =========================================================================
    [ObservableProperty]
    private bool _mostrarMenuHerramientas;

    [ObservableProperty]
    private bool _mostrarModalBateria;

    [ObservableProperty]
    private bool _mostrarModalGpsGeocercas;

    [ObservableProperty]
    private bool _mostrarModalOptimizacion;

    // --- MODAL 4: RESUMEN DE FINALIZACIÓN Y RENDICIÓN DE RUTA ---
    [ObservableProperty]
    private bool _mostrarModalFinalizarRuta;

    [ObservableProperty]
    private int _kpiEntregasExitosasCount;

    [ObservableProperty]
    private int _kpiIntentosFallidosCount;

    [ObservableProperty]
    private int _kpiBultosRetornoCount;

    [ObservableProperty]
    private bool _tieneParadasPendientesAlFinalizar;

    [ObservableProperty]
    private string _mensajeAdvertenciaPendientes = string.Empty;

    // --- BANNER / TOAST CORPORATIVO ---
    [ObservableProperty]
    private bool _mostrarToast;

    [ObservableProperty]
    private string _mensajeToast = string.Empty;

    [ObservableProperty]
    private Color _toastBgColor = Color.FromArgb("#16A34A");

    public void MostrarAvisoRapido(string mensaje, bool esExito = true)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MensajeToast = mensaje;
            ToastBgColor = esExito
                ? (Application.Current?.Resources.TryGetValue("SecondaryColor", out var sc) == true && sc is Color c ? c : Color.FromArgb("#16A34A"))
                : Color.FromArgb("#DC2626");
            MostrarToast = true;

            var timer = Application.Current?.Dispatcher?.CreateTimer();
            if (timer != null)
            {
                timer.Interval = TimeSpan.FromSeconds(2.5);
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    MostrarToast = false;
                };
                timer.Start();
            }
        });
    }

    // --- MODAL 1: BATERÍA Y MUESTREO ADAPTATIVO ---
    [ObservableProperty]
    private int _nivelBateriaPct = 100;

    [ObservableProperty]
    private double _nivelBateriaProgreso = 1.0;

    [ObservableProperty]
    private string _estadoBateriaTexto = "Descargando";

    [ObservableProperty]
    private string _modoMuestreoActualTexto = "Alta Frecuencia (> 50%)";

    [ObservableProperty]
    private string _intervaloDinamicoSegundosTexto = "15 s";

    [ObservableProperty]
    private string _ahorroEnergeticoEstimadoTexto = "~72%";

    [ObservableProperty]
    private int _lecturasGpsJornadaCount = 0;

    [ObservableProperty]
    private string _consumoEstimadoMahTexto = "~45 mAh/h";

    [ObservableProperty]
    private bool _esTramoBateriaAlto = true;

    [ObservableProperty]
    private bool _esTramoBateriaMedio = false;

    [ObservableProperty]
    private bool _esTramoBateriaBajo = false;

    // --- MODAL 2: ESTADO GPS Y GEOCERCAS ---
    [ObservableProperty]
    private string _estadoGpsBadgeTexto = "ACTIVO - Foreground Service";

    [ObservableProperty]
    private Color _estadoGpsBadgeBgColor = Color.FromArgb("#DCFCE7");

    [ObservableProperty]
    private Color _estadoGpsBadgeTextColor = Color.FromArgb("#15803D");

    [ObservableProperty]
    private string _ultimaCoordenadaModalTexto = "Sin coordenadas registradas";

    [ObservableProperty]
    private string _precisionGpsModalTexto = "High (Alta precisión)";

    [ObservableProperty]
    private string _horaUltimoEnvioTexto = "Sin transmisiones";

    [ObservableProperty]
    private string _radioEntradaTexto = "< 100 m";

    [ObservableProperty]
    private string _radioSalidaTexto = "> 150 m";

    public ObservableCollection<EventoGeocercaItemViewModel> EventosGeocercaJornada { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SinEventosGeocerca))]
    private bool _tieneEventosGeocerca;

    public bool SinEventosGeocerca => !TieneEventosGeocerca;

    // --- MODAL 3: OPTIMIZACIÓN 2-OPT ---
    [ObservableProperty]
    private string _optimizacionRutaOriginalTexto = string.Empty;

    [ObservableProperty]
    private string _optimizacionRutaOptimizadaTexto = string.Empty;

    [ObservableProperty]
    private string _optimizacionKmReducidosTexto = "0.0 km";

    [ObservableProperty]
    private string _optimizacionMinutosReducidosTexto = "0 min";

    [ObservableProperty]
    private string _optimizacionPorcentajeTexto = "-0%";

    [ObservableProperty]
    private bool _puedeAplicarOptimizacion = true;

    [ObservableProperty]
    private string _mensajeAvisoOptimizacion = string.Empty;

    private ResultadoOptimizacionRuta? _resultadoOptimizacionPendiente;
    private PosicionActualizadaEventArgs? _ultimaPosicionActualizada;

    public ObservableCollection<ParadaItemViewModel> Paradas { get; } = new();

    public RutaDiaViewModel() : this(null, null, null, null, null, null, null, null, null, null)
    {
    }

    public RutaDiaViewModel(
        ILocalDatabaseService? localDatabaseService,
        ISyncService? syncService,
        IGeolocalizacionTrackingService? trackingService,
        IGeofenceService? geofenceService = null,
        IAuthService? authService = null,
        IRutaOptimizationService? optimizationService = null,
        IBiometricAuthService? biometricAuthService = null,
        IOperadorThemeService? themeService = null,
        INotificacionLocalService? notificacionService = null,
        IRealtimeTrackingService? realtimeTrackingService = null)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;
        _trackingService = trackingService;
        _geofenceService = geofenceService;
        _authService = authService;
        _optimizationService = optimizationService ?? new RutaOptimizationService();
        _biometricAuthService = biometricAuthService;
        _themeService = themeService;
        _notificacionService = notificacionService;
        _realtimeTrackingService = realtimeTrackingService;
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
            UltimaCoordenadaTexto = _trackingService.UltimaCoordenadaTexto;
            UltimoEstadoServidorTexto = _trackingService.UltimoEstadoServidorTexto;
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
            try
            {
                ActualizarEstadoVisualConectividad(e.IsOnline, e.OperacionesPendientes);
                await RefrescarParadasDesdeSQLiteAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RutaDiaViewModel] Error en OnSyncStateChangedHandler: {ex.Message}");
            }
        });
    }

    private void OnTrackingStateChangedHandler(object? sender, bool activo)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsTrackingGpsActivo = activo;
            ForegroundServiceActivo = _trackingService?.ForegroundServiceActivo ?? false;
            ActualizarEstadoVisualTracking(activo);
        });
    }

    private void OnPosicionActualizadaHandler(object? sender, PosicionActualizadaEventArgs e)
    {
        _ultimaPosicionActualizada = e;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UltimaUbicacionTexto = $"{e.Latitud:F4}, {e.Longitud:F4} ({e.Timestamp.ToLocalTime():HH:mm:ss})";
            UltimaCoordenadaTexto = _trackingService?.UltimaCoordenadaTexto ?? $"GPS: Lat {e.Latitud:F4}, Lon {e.Longitud:F4} ({e.Timestamp.ToLocalTime():HH:mm:ss})";
            UltimoEstadoServidorTexto = _trackingService?.UltimoEstadoServidorTexto ?? "Servidor: Actualizado";
            ForegroundServiceActivo = _trackingService?.ForegroundServiceActivo ?? false;
            string estadoGps = ForegroundServiceActivo ? "GPS en vivo (Segundo plano)" : "GPS en vivo";
            if (e.TransmitidaSignalR)
            {
                EstadoTrackingTexto = estadoGps;
                TrackingDotColor = Color.FromArgb("#16A34A");
            }
            else
            {
                EstadoTrackingTexto = estadoGps;
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
        ForegroundServiceActivo = _trackingService?.ForegroundServiceActivo ?? false;
        if (activo)
        {
            EstadoTrackingTexto = ForegroundServiceActivo ? "GPS en vivo (Segundo plano)" : "GPS en vivo";
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

            // Solicitar permisos en el hilo principal de manera secuencial y segura antes de activar telemetría o GPS
            if (_trackingService is not null)
            {
                try
                {
                    await _trackingService.SolicitarPermisosUbicacionAsync();
                }
                catch (Exception exTracking)
                {
                    System.Diagnostics.Debug.WriteLine($"[RutaDiaViewModel] Error al solicitar permisos GPS: {exTracking.Message}");
                }
            }

            if (_syncService != null && _syncService.IsOnline)
            {
                try
                {
                    await _syncService.DescargarYActualizarRutasDelServidorAsync();
                }
                catch (Exception exSync)
                {
                    System.Diagnostics.Debug.WriteLine($"[RutaDiaViewModel] Error al descargar rutas del servidor: {exSync.Message}");
                }
            }

            try
            {
                await CargarRutasDesdeSQLiteAsync();
            }
            catch (Exception exLocal)
            {
                System.Diagnostics.Debug.WriteLine($"[RutaDiaViewModel] Error al cargar rutas desde SQLite: {exLocal.Message}");
            }

            int pendientes = 0;
            try
            {
                pendientes = _syncService != null
                    ? await _syncService.ObtenerCantidadPendientesAsync()
                    : (_localDatabaseService != null ? await _localDatabaseService.ObtenerCantidadPendientesAsync() : 0);
            }
            catch (Exception exPendientes)
            {
                System.Diagnostics.Debug.WriteLine($"[RutaDiaViewModel] Error consultando operaciones pendientes: {exPendientes.Message}");
            }

            ActualizarEstadoVisualConectividad(_syncService?.IsOnline ?? false, pendientes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RutaDiaViewModel] Error general en InicializarAsync: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ActualizarRutasServidorAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsRefreshing = true;
            IsBusy = true;

            if (_themeService != null)
            {
                await _themeService.SincronizarIdentidadVisualAsync();
            }

            if (_syncService != null && _syncService.IsOnline)
            {
                await _syncService.SincronizarOperacionesPendientesAsync();
                await _syncService.DescargarYActualizarRutasDelServidorAsync();
            }

            await CargarRutasDesdeSQLiteAsync();

            int pendientes = _syncService != null
                ? await _syncService.ObtenerCantidadPendientesAsync()
                : (_localDatabaseService != null ? await _localDatabaseService.ObtenerCantidadPendientesAsync() : 0);

            ActualizarEstadoVisualConectividad(_syncService?.IsOnline ?? false, pendientes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RutaDiaViewModel] Error al actualizar rutas: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    public async Task CargarRutasDesdeSQLiteAsync()
    {
        if (_localDatabaseService == null)
            return;

        var rutasLocales = await _localDatabaseService.ListarHojasDeRutaLocalesAsync();
        HojasDeRutaDisponibles.Clear();

        foreach (var r in rutasLocales)
        {
            string descEstado = r.Estado switch
            {
                "EnTransito" => "En Tránsito",
                "Despachada" => "Despachada",
                "EnCarga" => "En Carga",
                "Liberada" => "Liberada",
                "Finalizada" => "Todas las paradas completadas",
                "Rendida" => "Rendida en depósito",
                "Cerrada" => "Cerrada",
                _ => r.Estado
            };

            HojasDeRutaDisponibles.Add(new HojaDeRutaSelectorItem(
                r.Id,
                r.NumeroRuta,
                r.Estado,
                r.TotalParadas,
                $"{r.NumeroRuta} - {descEstado} ({r.TotalParadas} paradas)"
            ));
        }

        TieneMultiplesRutas = HojasDeRutaDisponibles.Count > 1;

        var rutaActiva = HojasDeRutaDisponibles.FirstOrDefault(r => r.Estado is "EnTransito" or "Despachada" or "EnCarga" or "Liberada" or "Finalizada");

        _actualizandoSelector = true;
        try
        {
            if (rutaActiva != null)
            {
                SinRutaAsignada = false;
                HojaDeRutaSeleccionada = rutaActiva;
                await CargarRutaEspecificaAsync(rutaActiva.Id);
            }
            else if (HojasDeRutaDisponibles.Count > 0 && HojasDeRutaDisponibles.All(r => r.Estado is "Cerrada" or "Rendida"))
            {
                SinRutaAsignada = true;
                Paradas.Clear();
                EstadoHojaDeRuta = HojasDeRutaDisponibles.First().Estado;
                HojaDeRutaSeleccionada = HojasDeRutaDisponibles.First();
                RecalcularProgreso();
                if (_trackingService is { IsTrackingActive: true })
                {
                    await _trackingService.DetenerTrackingAsync();
                }
            }
            else
            {
                SinRutaAsignada = true;
                Paradas.Clear();
                EstadoHojaDeRuta = "SinRuta";
                HojaDeRutaSeleccionada = null;
                RecalcularProgreso();
                if (_trackingService is { IsTrackingActive: true })
                {
                    await _trackingService.DetenerTrackingAsync();
                }
            }
        }
        finally
        {
            _actualizandoSelector = false;
        }
    }

    public async Task CargarRutaEspecificaAsync(Guid rutaId)
    {
        if (_localDatabaseService == null)
            return;

        try
        {
            var local = await _localDatabaseService.ObtenerRutaActivaLocalAsync(rutaId);
            if (local != null)
            {
                Paradas.Clear();
                foreach (var p in local.Paradas)
                {
                    Paradas.Add(p);
                }
                EstadoHojaDeRuta = local.Ruta.Estado;
                FechaHojaRuta = local.Ruta.Fecha.ToString("dd/MM/yyyy");
                SinRutaAsignada = false;
                AplicarEstadosGeocercaEnParadas();
                RecalcularProgreso();
                await EvaluarYActualizarTrackingAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RutaDiaViewModel] Error al cargar ruta {rutaId}: {ex.Message}");
        }
    }

    public async Task RefrescarParadasDesdeSQLiteAsync()
    {
        if (_localDatabaseService is null)
            return;

        if (!await _uiRefreshLock.WaitAsync(0))
            return;

        try
        {
            var rutaId = HojaDeRutaSeleccionada?.Id;
            var local = await _localDatabaseService.ObtenerRutaActivaLocalAsync(rutaId);
            if (local == null || local.Ruta == null || local.Ruta.Estado is "Cerrada" or "Rendida")
            {
                await CargarRutasDesdeSQLiteAsync();
                return;
            }

            if (local.Paradas.Count > 0)
            {
                Paradas.Clear();
                foreach (var p in local.Paradas)
                {
                    Paradas.Add(p);
                }
                AplicarEstadosGeocercaEnParadas();
            }
            EstadoHojaDeRuta = local.Ruta.Estado;
            FechaHojaRuta = local.Ruta.Fecha.ToString("dd/MM/yyyy");
            SinRutaAsignada = false;
            RecalcularProgreso();
            await EvaluarYActualizarTrackingAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RutaDiaViewModel] Error en RefrescarParadasDesdeSQLiteAsync: {ex.Message}");
        }
        finally
        {
            _uiRefreshLock.Release();
        }
    }

    public async Task EvaluarYActualizarTrackingAsync()
    {
        if (_trackingService == null)
            return;

        var rutaLocal = _localDatabaseService != null ? await _localDatabaseService.ObtenerRutaActivaLocalAsync() : null;
        if (rutaLocal?.Ruta == null || EstadoHojaDeRuta is "Cerrada" or "Rendida" || rutaLocal.Ruta.Estado is "Cerrada" or "Rendida")
        {
            if (_trackingService.IsTrackingActive)
            {
                await _trackingService.DetenerTrackingAsync();
            }
            return;
        }

        if (_authService?.RepartidorId is { } repartidorId && repartidorId != Guid.Empty)
        {
            var rutaId = rutaLocal.Ruta.Id;

            if (!_trackingService.IsTrackingActive)
            {
                await _trackingService.IniciarTrackingAsync(rutaId, repartidorId);
            }
            else
            {
                _trackingService.HojaDeRutaIdActiva = rutaId;
                _trackingService.RepartidorIdActivo = repartidorId;
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
            string motivo = IsOfflineSimulado ? "Sin conexión (manual)" : "Modo Offline";
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
            TotalParadasCount = 0;
            ParadasPendientesCount = 0;
            ParadasCompletadasCount = 0;
            ProgresoRuta = 0;
            ResumenProgresoTexto = "0 de 0 paradas completadas";
            ProgresoPorcentajeTexto = "0%";
            PuedeFinalizarRuta = false;
            return;
        }

        int completadas = Paradas.Count(p => p.Estado is "Entregado" or "Fallido" or "EnDevolucion");
        int total = Paradas.Count;
        int pendientes = Paradas.Count(p => p.Estado == "Pendiente");
        TotalParadasCount = total;
        ParadasPendientesCount = pendientes;
        ParadasCompletadasCount = completadas;
        ProgresoRuta = (double)completadas / total;
        ResumenProgresoTexto = $"{completadas} de {total} paradas completadas";
        ProgresoPorcentajeTexto = $"{Math.Round(ProgresoRuta * 100)}%";

        PuedeFinalizarRuta = total > 0 && pendientes == 0 && EstadoHojaDeRuta is not ("Cerrada" or "Rendida");

        if (total > 0 && completadas == total && _trackingService is { IsTrackingActive: true })
        {
            _ = _trackingService.DetenerTrackingAsync();
        }
    }

    [RelayCommand]
    private void MostrarColaOffline()
    {
        AlternarMenuHerramientas();
    }

    [RelayCommand]
    private async Task CerrarSesionAsync()
    {
        if (_trackingService is { IsTrackingActive: true })
        {
            await _trackingService.DetenerTrackingAsync();
        }

        if (_realtimeTrackingService is not null)
        {
            await _realtimeTrackingService.StopAsync();
        }

        if (_authService is not null)
        {
            await _authService.CerrarSesionAsync();
        }
        else if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }

    [RelayCommand]
    public void SolicitarFinalizarHojaDeRuta()
    {
        if (IsBusy || Paradas.Count == 0)
            return;

        int total = Paradas.Count;
        int entregadas = Paradas.Count(p => p.Estado == "Entregado");
        int fallidas = Paradas.Count(p => p.Estado == "Fallido");
        int devolucion = Paradas.Count(p => p.Estado == "EnDevolucion");
        int pendientes = Paradas.Count(p => p.Estado == "Pendiente");

        var bultosRetorno = Paradas
            .Where(p => p.Estado is "Fallido" or "EnDevolucion")
            .SelectMany(p => p.Bultos)
            .Select(b => b.Codigo)
            .Distinct()
            .ToList();

        KpiEntregasExitosasCount = entregadas;
        KpiIntentosFallidosCount = fallidas + devolucion;
        KpiBultosRetornoCount = bultosRetorno.Count;

        if (pendientes > 0)
        {
            TieneParadasPendientesAlFinalizar = true;
            MensajeAdvertenciaPendientes = $"Atención: Aún restan {pendientes} parada(s) en estado Pendiente. Al cerrar la ruta retornarán a depósito como no entregadas.";
        }
        else
        {
            TieneParadasPendientesAlFinalizar = false;
            MensajeAdvertenciaPendientes = string.Empty;
        }

        MostrarModalFinalizarRuta = true;
    }

    [RelayCommand]
    public void CerrarModalFinalizarRuta()
    {
        MostrarModalFinalizarRuta = false;
    }

    [RelayCommand]
    public async Task ConfirmarCierreConBiometriaAsync()
    {
        MostrarModalFinalizarRuta = false;

        if (_biometricAuthService != null)
        {
            var autorizado = await _biometricAuthService.ValidarIdentidadAsync(
                "Autorizar Rendición de Ruta",
                "Verifique su identidad biométrica para rendir la hoja de ruta y retornar a depósito.");

            if (!autorizado)
            {
                MostrarAvisoRapido("Verificación biométrica cancelada.", esExito: false);
                return;
            }
        }

        try
        {
            IsBusy = true;

            var bultosRetorno = Paradas
                .Where(p => p.Estado is "Fallido" or "EnDevolucion")
                .SelectMany(p => p.Bultos)
                .Select(b => b.Codigo)
                .Distinct()
                .ToList();

            var rutaId = HojaDeRutaSeleccionada?.Id ?? Paradas.FirstOrDefault()?.HojaDeRutaId ?? Guid.Empty;

            if (_trackingService is { IsTrackingActive: true })
            {
                await _trackingService.DetenerTrackingAsync();
            }

            if (_syncService != null && rutaId != Guid.Empty)
            {
                await _syncService.FinalizarHojaDeRutaAsync(rutaId, bultosRetorno, "Rendición de hoja de ruta completada en depósito");
            }
            else if (_localDatabaseService != null && rutaId != Guid.Empty)
            {
                await _localDatabaseService.FinalizarHojaDeRutaLocalAsync(rutaId);
            }

            EstadoHojaDeRuta = "Cerrada";
            MostrarAvisoRapido("Hoja de ruta finalizada y rendida en depósito");

            // Consultar inmediatamente nuevas rutas asignadas en el servidor
            if (_syncService != null && _syncService.IsOnline)
            {
                await _syncService.DescargarYActualizarRutasDelServidorAsync();
            }

            await CargarRutasDesdeSQLiteAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RutaDiaViewModel] Error al finalizar ruta: {ex.Message}");
            MostrarAvisoRapido("Error al finalizar hoja de ruta", esExito: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task FinalizarHojaDeRutaAsync()
    {
        SolicitarFinalizarHojaDeRuta();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RendirHojaDeRutaAsync()
    {
        SolicitarFinalizarHojaDeRuta();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task IrAParadaAsync(ParadaItemViewModel? parada)
    {
        if (parada is null)
            return;

        if (RecorridoNoIniciado || parada.EnPrecarga)
        {
            MostrarAvisoRapido("Ruta en precarga: diríjase a Carga para escanear los bultos.", esExito: false);
            return;
        }

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

        if (RecorridoNoIniciado || parada.EnPrecarga)
        {
            MostrarAvisoRapido("Ruta en precarga: diríjase a Carga para escanear los bultos.", esExito: false);
            return;
        }

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
        if (_trackingService is null)
            return;

        if (_trackingService.IsTrackingActive)
        {
            await _trackingService.DetenerTrackingAsync();
            ActualizarEstadoVisualTracking(false);
        }
        else
        {
            var rutaLocal = _localDatabaseService != null ? await _localDatabaseService.ObtenerRutaActivaLocalAsync() : null;
            var rutaId = rutaLocal?.Ruta?.Id ?? (Paradas.FirstOrDefault()?.HojaDeRutaId ?? Guid.Empty);
            var repartidorId = _authService?.RepartidorId ?? Guid.Empty;

            if (repartidorId != Guid.Empty)
            {
                await _trackingService.IniciarTrackingAsync(rutaId, repartidorId);
                ActualizarEstadoVisualTracking(true);
            }
        }
    }

    [RelayCommand]
    private void AlternarMenuHerramientas()
    {
        MostrarMenuHerramientas = !MostrarMenuHerramientas;
        if (MostrarMenuHerramientas)
        {
            MostrarModalBateria = false;
            MostrarModalGpsGeocercas = false;
            MostrarModalOptimizacion = false;
        }
    }

    [RelayCommand]
    private void CerrarModales()
    {
        MostrarMenuHerramientas = false;
        MostrarModalBateria = false;
        MostrarModalGpsGeocercas = false;
        MostrarModalOptimizacion = false;
    }

    [RelayCommand]
    private void AbrirModalBateria()
    {
        MostrarMenuHerramientas = false;
        CargarTelemetriaBateria();
        MostrarModalBateria = true;
    }

    private void CargarTelemetriaBateria()
    {
        if (_trackingService is null)
            return;

        var telemetria = _trackingService.ObtenerTelemetriaBateriaActual();
        NivelBateriaPct = telemetria.PorcentajeBateria;
        NivelBateriaProgreso = Math.Clamp(telemetria.PorcentajeBateria / 100.0, 0.0, 1.0);
        EstadoBateriaTexto = telemetria.EstadoBateria;
        ModoMuestreoActualTexto = telemetria.PerfilAdaptativoActual;
        IntervaloDinamicoSegundosTexto = $"{telemetria.IntervaloSegundosActual} s";
        AhorroEnergeticoEstimadoTexto = $"~{telemetria.AhorroEstimadoPorcentaje:F0}%";
        LecturasGpsJornadaCount = telemetria.LecturasGpsRealizadas;
        ConsumoEstimadoMahTexto = $"~{telemetria.ConsumoEstimadoMahHora:F0} mAh/h";

        EsTramoBateriaAlto = telemetria.PorcentajeBateria > 50;
        EsTramoBateriaMedio = telemetria.PorcentajeBateria >= 20 && telemetria.PorcentajeBateria <= 50;
        EsTramoBateriaBajo = telemetria.PorcentajeBateria < 20;
    }

    [RelayCommand]
    private async Task AbrirModalGpsGeocercasAsync()
    {
        MostrarMenuHerramientas = false;
        await CargarTelemetriaGpsGeocercasAsync();
        MostrarModalGpsGeocercas = true;
    }

    private async Task CargarTelemetriaGpsGeocercasAsync()
    {
        if (_trackingService is null)
            return;

        bool activo = _trackingService.IsTrackingActive;
        EstadoGpsBadgeTexto = activo
            ? (_trackingService.ForegroundServiceActivo ? "ACTIVO - Foreground Service" : "ACTIVO")
            : "PAUSADO";

        EstadoGpsBadgeBgColor = activo ? Color.FromArgb("#DCFCE7") : Color.FromArgb("#FEE2E2");
        EstadoGpsBadgeTextColor = activo ? Color.FromArgb("#15803D") : Color.FromArgb("#B91C1C");

        if (_ultimaPosicionActualizada is not null)
        {
            UltimaCoordenadaModalTexto = $"{_ultimaPosicionActualizada.Latitud:F5}, {_ultimaPosicionActualizada.Longitud:F5}";
            PrecisionGpsModalTexto = _ultimaPosicionActualizada.PrecisionMetros.HasValue
                ? $"{_ultimaPosicionActualizada.PrecisionMetros.Value:F1} m"
                : "Normal";
            HoraUltimoEnvioTexto = _ultimaPosicionActualizada.Timestamp.ToLocalTime().ToString("HH:mm:ss");
        }
        else if (_trackingService.UltimaUbicacionConocida is not null)
        {
            var loc = _trackingService.UltimaUbicacionConocida;
            UltimaCoordenadaModalTexto = $"{loc.Latitude:F5}, {loc.Longitude:F5}";
            PrecisionGpsModalTexto = loc.Accuracy.HasValue ? $"{loc.Accuracy.Value:F1} m" : "Normal";
            HoraUltimoEnvioTexto = DateTime.Now.ToString("HH:mm:ss");
        }
        else
        {
            UltimaCoordenadaModalTexto = UltimaCoordenadaTexto;
            PrecisionGpsModalTexto = "Normal";
            HoraUltimoEnvioTexto = DateTime.Now.ToString("HH:mm:ss");
        }

        RadioEntradaTexto = "< 100 m";
        RadioSalidaTexto = "> 150 m";

        EventosGeocercaJornada.Clear();
        if (_localDatabaseService is not null)
        {
            var eventos = await _localDatabaseService.ObtenerEventosGeocercaAsync(12);
            foreach (var ev in eventos)
            {
                var esLlegada = string.Equals(ev.TipoEvento, "LLEGADA_GEOCERCA", StringComparison.OrdinalIgnoreCase);
                EventosGeocercaJornada.Add(new EventoGeocercaItemViewModel
                {
                    HoraTexto = ev.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"),
                    CodigoSeguimiento = string.IsNullOrWhiteSpace(ev.CodigoSeguimiento) ? "Pto. Control" : ev.CodigoSeguimiento,
                    TipoEventoTexto = esLlegada ? "LLEGADA" : "SALIDA",
                    TipoEventoBadgeBg = esLlegada ? Color.FromArgb("#DCFCE7") : Color.FromArgb("#FEF3C7"),
                    TipoEventoBadgeTextColor = esLlegada ? Color.FromArgb("#15803D") : Color.FromArgb("#92400E"),
                    TienePermanencia = ev.TiempoPermanenciaSegundos.HasValue && ev.TiempoPermanenciaSegundos > 0,
                    PermanenciaTexto = ev.TiempoPermanenciaSegundos.HasValue && ev.TiempoPermanenciaSegundos > 0
                        ? $"{ev.TiempoPermanenciaSegundos.Value}s"
                        : string.Empty
                });
            }
        }

        TieneEventosGeocerca = EventosGeocercaJornada.Count > 0;
        OnPropertyChanged(nameof(SinEventosGeocerca));
    }

    [RelayCommand]
    private async Task ForzarLecturaGpsDesdeModalAsync()
    {
        await ForzarEnvioGpsAhoraAsync();
        await CargarTelemetriaGpsGeocercasAsync();
    }

    [RelayCommand]
    private async Task AlternarModoOfflineDesdeMenuAsync()
    {
        MostrarMenuHerramientas = false;
        if (_syncService is null) return;

        await _syncService.AlternarModoOfflineSimuladoAsync();
        int pendientes = await _syncService.ObtenerCantidadPendientesAsync();
        ActualizarEstadoVisualConectividad(_syncService.IsOnline, pendientes);
        await RefrescarParadasDesdeSQLiteAsync();
    }

    [RelayCommand]
    private async Task RendirHojaDeRutaDesdeMenuAsync()
    {
        MostrarMenuHerramientas = false;
        await RendirHojaDeRutaAsync();
    }

    [RelayCommand]
    private async Task ProbarNotificacionOperativaAsync()
    {
        MostrarMenuHerramientas = false;
        if (_notificacionService is null)
        {
            return;
        }

        await _notificacionService.SolicitarPermisosNotificacionAsync();
        var pendientes = Math.Max(1, ParadasPendientesCount);
        await _notificacionService.MostrarNotificacionOperativaAsync(
            "Nueva Hoja de Ruta Asignada",
            $"Se habilito la ruta HR-{DateTime.Today:yyyyMMdd}-01 con {pendientes} paradas pendientes. Ingresa a la seccion Carga para verificar los bultos.",
            "RUTA");
    }

    [RelayCommand]
    private async Task SincronizarTodoDesdeMenuAsync()
    {
        MostrarMenuHerramientas = false;
        await ActualizarRutasServidorAsync();
    }

    [RelayCommand]
    private async Task EjecutarOptimizacionDesdeMenuAsync()
    {
        MostrarMenuHerramientas = false;
        await OptimizarRutaAsync();
    }

    [RelayCommand]
    private async Task ForzarEnvioGpsDesdeMenuAsync()
    {
        MostrarMenuHerramientas = false;
        await ForzarEnvioGpsAhoraAsync();
        MostrarAvisoRapido("Coordenadas GPS transmitidas a la central.");
    }

    [RelayCommand]
    private void GestionarGeocercasYBateria()
    {
        AbrirModalBateria();
    }

    [RelayCommand]
    private async Task ForzarEnvioGpsAhoraAsync()
    {
        if (_trackingService == null)
            return;

        var rutaLocal = _localDatabaseService != null ? await _localDatabaseService.ObtenerRutaActivaLocalAsync() : null;
        var rutaId = rutaLocal?.Ruta?.Id ?? (Paradas.FirstOrDefault()?.HojaDeRutaId ?? Guid.Empty);
        var repartidorId = _authService?.RepartidorId ?? Guid.Empty;

        UltimoEstadoServidorTexto = "Transmitiendo...";

        var (exito, detalle) = await _trackingService.ForzarEnvioGpsInmediatoAsync(rutaId, repartidorId);
        UltimaCoordenadaTexto = _trackingService.UltimaCoordenadaTexto;
        UltimoEstadoServidorTexto = _trackingService.UltimoEstadoServidorTexto;
    }

    [RelayCommand]
    private async Task OptimizarRutaAsync()
    {
        if (IsBusy)
            return;

        var paradasActuales = Paradas.ToList();
        int cantidadPendientes = paradasActuales.Count(p => p.Estado == "Pendiente");

        if (cantidadPendientes < 2)
        {
            OptimizacionRutaOriginalTexto = "Menos de 2 paradas pendientes";
            OptimizacionRutaOptimizadaTexto = "Sin cambios";
            OptimizacionKmReducidosTexto = "0.0 km";
            OptimizacionMinutosReducidosTexto = "0 min";
            OptimizacionPorcentajeTexto = "-0%";
            PuedeAplicarOptimizacion = false;
            MensajeAvisoOptimizacion = "Se requieren al menos 2 paradas en estado Pendiente para ejecutar la optimización del recorrido.";
            MostrarModalOptimizacion = true;
            return;
        }

        try
        {
            IsBusy = true;

            double? latActual = null;
            double? lonActual = null;
            try
            {
                if (_trackingService?.UltimaUbicacionConocida is not null)
                {
                    latActual = _trackingService.UltimaUbicacionConocida.Latitude;
                    lonActual = _trackingService.UltimaUbicacionConocida.Longitude;
                }
                else
                {
                    var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                    if (status == PermissionStatus.Granted)
                    {
                        var lastLocation = await Geolocation.Default.GetLastKnownLocationAsync();
                        if (lastLocation != null)
                        {
                            latActual = lastLocation.Latitude;
                            lonActual = lastLocation.Longitude;
                        }
                    }
                }
            }
            catch
            {
                // Si el dispositivo está offline o sin permiso GPS activo, RutaOptimizationService usa el punto de partida operativo
            }

            var resultado = await _optimizationService.OptimizarOrdenParadasAsync(
                paradasActuales,
                latActual,
                lonActual);

            _resultadoOptimizacionPendiente = resultado;
            OptimizacionRutaOriginalTexto = $"{resultado.DistanciaBaseKm:F1} km (~{resultado.TiempoEstimadoBaseMinutos} min)";
            OptimizacionRutaOptimizadaTexto = $"{resultado.DistanciaOptimizadaKm:F1} km (~{resultado.TiempoEstimadoOptimizadoMinutos} min)";
            OptimizacionKmReducidosTexto = $"-{resultado.AhorroKm:F1} km";
            OptimizacionMinutosReducidosTexto = $"-{resultado.AhorroMinutos} min";
            OptimizacionPorcentajeTexto = $"-{resultado.PorcentajeAhorro:F1}%";
            PuedeAplicarOptimizacion = resultado.ModificaOrdenActual;
            MensajeAvisoOptimizacion = resultado.ModificaOrdenActual
                ? "Se ha detectado una ruta mas eficiente mediante el algoritmo 2-Opt."
                : "La secuencia actual ya es la optima segun el algoritmo 2-Opt.";

            MostrarModalOptimizacion = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AplicarOptimizacionDesdeModalAsync()
    {
        if (_resultadoOptimizacionPendiente is null)
        {
            MostrarModalOptimizacion = false;
            return;
        }

        try
        {
            IsBusy = true;
            await AplicarSecuenciaParadasAsync(_resultadoOptimizacionPendiente.SecuenciaOptimizada);

            ResumenCriterioEstandarTexto = $"{_resultadoOptimizacionPendiente.DistanciaBaseKm:F1} km (~{_resultadoOptimizacionPendiente.TiempoEstimadoBaseMinutos} min)";
            ResumenRutaOptimizadaTexto = $"{_resultadoOptimizacionPendiente.DistanciaOptimizadaKm:F1} km (~{_resultadoOptimizacionPendiente.TiempoEstimadoOptimizadoMinutos} min)";
            ResumenAhorroRecorridoTexto = $"-{_resultadoOptimizacionPendiente.PorcentajeAhorro:F1}% (-{_resultadoOptimizacionPendiente.AhorroKm:F1} km)";
            ResumenTiempoReducidoTexto = $"-{_resultadoOptimizacionPendiente.AhorroMinutos} minutos";
            ResumenSecuenciaAplicadaTexto = string.Join(" -> ", _resultadoOptimizacionPendiente.SecuenciaOptimizada.Select(p => p.CodigoSeguimiento));
            MostrarResumenOptimizacion = true;
        }
        finally
        {
            IsBusy = false;
            MostrarModalOptimizacion = false;
            _resultadoOptimizacionPendiente = null;
        }
    }

    [RelayCommand]
    private void DescartarOptimizacionModal()
    {
        MostrarModalOptimizacion = false;
        _resultadoOptimizacionPendiente = null;
    }

    [RelayCommand]
    private async Task RestaurarOrdenEstandarAsync()
    {
        if (IsBusy || Paradas.Count == 0)
            return;

        try
        {
            IsBusy = true;
            var secuenciaEstandar = _optimizationService.ConstruirSecuenciaEstandarDespacho(Paradas.ToList());
            await AplicarSecuenciaParadasAsync(secuenciaEstandar);
            MostrarResumenOptimizacion = false;
            MostrarAvisoRapido("Orden inicial de ruta restaurado");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OcultarResumenOptimizacion()
    {
        MostrarResumenOptimizacion = false;
    }

    private async Task AplicarSecuenciaParadasAsync(IReadOnlyList<ParadaItemViewModel> nuevaSecuencia)
    {
        for (int i = 0; i < nuevaSecuencia.Count; i++)
        {
            int nuevoOrden = i + 1;
            nuevaSecuencia[i].Orden = nuevoOrden;
            nuevaSecuencia[i].HoraEstimada = $"{8 + nuevoOrden:D2}:30";
        }

        Paradas.Clear();
        foreach (var parada in nuevaSecuencia)
        {
            Paradas.Add(parada);
        }

        if (_localDatabaseService is not null)
        {
            await _localDatabaseService.ActualizarSecuenciaParadasAsync(nuevaSecuencia);
        }

        AplicarEstadosGeocercaEnParadas();
        RecalcularProgreso();
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
