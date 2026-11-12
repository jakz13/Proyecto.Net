using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.Local;
using UltimaMilla.Mobile.Services.Auth;
using UltimaMilla.Mobile.Services.Optimization;
using UltimaMilla.Mobile.Services.Security;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.Services.Tracking;
using UltimaMilla.Mobile.Views.Ruta;

namespace UltimaMilla.Mobile.ViewModels.Ruta;

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

    public ObservableCollection<HojaDeRutaSelectorItem> HojasDeRutaDisponibles { get; } = new();

    async partial void OnHojaDeRutaSeleccionadaChanged(HojaDeRutaSelectorItem? value)
    {
        if (value != null && !IsBusy)
        {
            await CargarRutaEspecificaAsync(value.Id);
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

    public ObservableCollection<ParadaItemViewModel> Paradas { get; } = new();

    public RutaDiaViewModel() : this(null, null, null, null, null, null, null)
    {
    }

    public RutaDiaViewModel(
        ILocalDatabaseService? localDatabaseService,
        ISyncService? syncService,
        IGeolocalizacionTrackingService? trackingService,
        IGeofenceService? geofenceService = null,
        IAuthService? authService = null,
        IRutaOptimizationService? optimizationService = null,
        IBiometricAuthService? biometricAuthService = null)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;
        _trackingService = trackingService;
        _geofenceService = geofenceService;
        _authService = authService;
        _optimizationService = optimizationService ?? new RutaOptimizationService();
        _biometricAuthService = biometricAuthService;
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
            ActualizarEstadoVisualConectividad(e.IsOnline, e.OperacionesPendientes);
            await RefrescarParadasDesdeSQLiteAsync();
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
            HojaDeRutaSeleccionada = null;
            RecalcularProgreso();
            if (_trackingService is { IsTrackingActive: true })
            {
                await _trackingService.DetenerTrackingAsync();
            }
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

        try
        {
            var rutaId = HojaDeRutaSeleccionada?.Id;
            var local = await _localDatabaseService.ObtenerRutaActivaLocalAsync(rutaId);
            if (local != null && local.Paradas.Count > 0)
            {
                Paradas.Clear();
                foreach (var p in local.Paradas)
                {
                    Paradas.Add(p);
                }
                AplicarEstadosGeocercaEnParadas();
            }
            if (local?.Ruta != null)
            {
                EstadoHojaDeRuta = local.Ruta.Estado;
                FechaHojaRuta = local.Ruta.Fecha.ToString("dd/MM/yyyy");
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

        if (_authService?.RepartidorId is { } repartidorId && repartidorId != Guid.Empty)
        {
            var rutaLocal = _localDatabaseService != null ? await _localDatabaseService.ObtenerRutaActivaLocalAsync() : null;
            var rutaId = rutaLocal?.Ruta?.Id ?? (Paradas.FirstOrDefault()?.HojaDeRutaId ?? Guid.Empty);

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
        else if (EstadoHojaDeRuta is "Cerrada" or "Rendida")
        {
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
            ProgresoRuta = 0;
            ResumenProgresoTexto = "0 de 0 paradas completadas";
            ProgresoPorcentajeTexto = "0%";
            PuedeFinalizarRuta = false;
            return;
        }

        int completadas = Paradas.Count(p => p.Estado is "Entregado" or "Fallido" or "EnDevolucion");
        int total = Paradas.Count;
        int pendientes = Paradas.Count(p => p.Estado == "Pendiente");
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
    private async Task MostrarColaOfflineAsync()
    {
        if (Shell.Current is null)
            return;

        int pendientes = _syncService != null
            ? await _syncService.ObtenerCantidadPendientesAsync()
            : (_localDatabaseService != null ? await _localDatabaseService.ObtenerCantidadPendientesAsync() : 0);

        ColaOfflineCount = pendientes;

        string opcionToggle = (_syncService?.IsOfflineSimulado ?? false)
            ? "Volver a conectarse"
            : "Trabajar sin conexión";

        const string opcionRendirRuta = "Informar llegada al depósito (rendición)";
        const string opcionSync = "Sincronizar ahora";
        const string opcionCerrarSesion = "Cerrar sesión";

        var seleccion = await Shell.Current.DisplayActionSheetAsync(
            $"Conectividad y cola ({pendientes} pendientes)",
            "Cerrar",
            null,
            opcionToggle,
            opcionRendirRuta,
            opcionSync,
            opcionCerrarSesion);

        if (seleccion == opcionRendirRuta)
        {
            await RendirHojaDeRutaAsync();
        }
        else if (seleccion == opcionToggle && _syncService is not null)
        {
            await _syncService.AlternarModoOfflineSimuladoAsync();

            if (_syncService.IsOnline)
            {
                var syncRes = await _syncService.SincronizarOperacionesPendientesAsync();
                int pendientesRestantes = await _syncService.ObtenerCantidadPendientesAsync();
                ActualizarEstadoVisualConectividad(true, pendientesRestantes);
                await RefrescarParadasDesdeSQLiteAsync();

                string mensaje = $"Conexión restablecida.\n{syncRes.Mensaje}";
                await Shell.Current.DisplayAlertAsync("Sincronización al volver a línea", mensaje, "Aceptar");
            }
            else
            {
                int p = await _syncService.ObtenerCantidadPendientesAsync();
                ActualizarEstadoVisualConectividad(false, p);
                await Shell.Current.DisplayAlertAsync(
                    "Modo de Conexión",
                    "Trabajando sin conexión. Las operaciones se guardan en el dispositivo (base cifrada) y se envían cuando se vuelva a conectar.",
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
                    "El dispositivo no tiene conexión. Vuelva a conectarse para enviar la cola al servidor.",
                    "Entendido");
                return;
            }

            var resultado = await _syncService.SincronizarOperacionesPendientesAsync();
            int pendientesRestantes = await _syncService.ObtenerCantidadPendientesAsync();
            ActualizarEstadoVisualConectividad(true, pendientesRestantes);
            await Shell.Current.DisplayAlertAsync("Sincronización de Cola", resultado.Mensaje, "Aceptar");
            await RefrescarParadasDesdeSQLiteAsync();
        }
        else if (seleccion == opcionCerrarSesion)
        {
            await CerrarSesionAsync();
        }
    }

    [RelayCommand]
    private async Task CerrarSesionAsync()
    {
        if (_trackingService is { IsTrackingActive: true })
        {
            await _trackingService.DetenerTrackingAsync();
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
    private async Task FinalizarHojaDeRutaAsync()
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

        string mensajeResumen = $"Resumen de Hoja de Ruta:\n\n" +
            $"- Total de paradas: {total}\n" +
            $"- Entregadas con éxito: {entregadas}\n" +
            $"- Fallidas / En Devolución: {fallidas + devolucion}\n" +
            $"- Bultos a retornar a depósito: {bultosRetorno.Count}\n\n";

        if (pendientes > 0)
        {
            mensajeResumen += $"Atención: Aún restan {pendientes} parada(s) en estado Pendiente.\n¿Desea cerrar la ruta y retornar a depósito?";
        }
        else
        {
            mensajeResumen += "¿Confirma el cierre y rendición de la hoja de ruta en depósito?";
        }

        if (Shell.Current is not null)
        {
            bool confirmar = await Shell.Current.DisplayAlertAsync(
                "Finalizar Hoja de Ruta",
                mensajeResumen,
                "Confirmar Cierre",
                "Cancelar");

            if (!confirmar)
                return;
        }

        if (_biometricAuthService != null)
        {
            var autorizado = await _biometricAuthService.ValidarIdentidadAsync(
                "Autorizar Rendición de Ruta",
                "Verifique su identidad biométrica para rendir la hoja de ruta y retornar a depósito.");

            if (!autorizado)
            {
                if (Shell.Current is not null)
                {
                    await Shell.Current.DisplayAlertAsync(
                        "Operación Cancelada",
                        "No se completó la verificación biométrica. La rendición de la hoja de ruta fue cancelada.",
                        "Entendido");
                }
                return;
            }
        }

        try
        {
            IsBusy = true;

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

            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Hoja de Ruta Finalizada",
                    "La hoja de ruta ha sido cerrada y rendida con éxito.",
                    "Aceptar");
            }

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
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RendirHojaDeRutaAsync()
    {
        await FinalizarHojaDeRutaAsync();
    }

    [RelayCommand]
    private async Task IrAParadaAsync(ParadaItemViewModel? parada)
    {
        if (parada is null)
            return;

        if (RecorridoNoIniciado || parada.EnPrecarga)
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Pendiente de Carga en Depósito",
                    "Los bultos de esta ruta aún no fueron escaneados ni despachados. Diríjase a la pestaña Carga para escanear los bultos y pasar los envíos a En Tránsito.",
                    "Entendido");
            }
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
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Pendiente de Carga en Depósito",
                    "Los bultos de esta ruta aún no fueron escaneados ni despachados. Diríjase a la pestaña Carga para escanear los bultos y pasar los envíos a En Tránsito.",
                    "Entendido");
            }
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
        await GestionarGeocercasYBateriaAsync();
    }

    [RelayCommand]
    private async Task GestionarGeocercasYBateriaAsync()
    {
        if (Shell.Current is null || _trackingService is null)
            return;

        const string opcionAnalisis = "Ver Análisis de Batería e Historial de Geocercas";
        string opcionToggleGps = _trackingService.IsTrackingActive
            ? "Pausar Rastreo GPS Automático"
            : "Activar Rastreo GPS Automático";

        var seleccion = await Shell.Current.DisplayActionSheetAsync(
            "Geocercas y consumo de batería",
            "Cerrar",
            null,
            opcionAnalisis,
            opcionToggleGps);

        if (seleccion == opcionAnalisis)
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
                var rutaLocal = _localDatabaseService != null ? await _localDatabaseService.ObtenerRutaActivaLocalAsync() : null;
                var rutaId = rutaLocal?.Ruta?.Id ?? (Paradas.FirstOrDefault()?.HojaDeRutaId ?? Guid.Empty);
                var repartidorId = _authService?.RepartidorId ?? Guid.Empty;

                if (repartidorId == Guid.Empty)
                {
                    await Shell.Current.DisplayAlertAsync(
                        "Rastreo GPS",
                        "No se identificó la sesión del repartidor: inicie sesión para activar el rastreo.",
                        "Entendido");
                    return;
                }

                await _trackingService.IniciarTrackingAsync(rutaId, repartidorId);
                string modoSegundoPlano = _trackingService.ForegroundServiceActivo
                    ? "\nModo segundo plano: Activo con soporte de pantalla apagada (Servicio en primer plano Android)."
                    : string.Empty;
                await Shell.Current.DisplayAlertAsync(
                    "Tracking GPS en Vivo",
                    $"Rastreo adaptativo iniciado.\nPerfil actual: {_trackingService.PerfilAdaptativoActual}\nIntervalo: {_trackingService.IntervaloTracking.TotalSeconds}s.{modoSegundoPlano}",
                    "Entendido");
            }
        }
    }

    [RelayCommand]
    private async Task ForzarEnvioGpsAhoraAsync()
    {
        if (_trackingService == null)
            return;

        var rutaLocal = _localDatabaseService != null ? await _localDatabaseService.ObtenerRutaActivaLocalAsync() : null;
        var rutaId = rutaLocal?.Ruta?.Id ?? (Paradas.FirstOrDefault()?.HojaDeRutaId ?? Guid.Empty);
        var repartidorId = _authService?.RepartidorId ?? Guid.Empty;

        UltimoEstadoServidorTexto = "Servidor: Transmitiendo...";

        var (exito, detalle) = await _trackingService.ForzarEnvioGpsInmediatoAsync(rutaId, repartidorId);
        UltimaCoordenadaTexto = _trackingService.UltimaCoordenadaTexto;
        UltimoEstadoServidorTexto = _trackingService.UltimoEstadoServidorTexto;

        if (Shell.Current != null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Telemetria GPS",
                detalle,
                "Entendido");
        }
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
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Optimización de Ruta",
                    "Se requieren al menos 2 paradas en estado Pendiente para ejecutar la optimización de recorrido. Las paradas ya completadas (Entregado o Fallido) permanecen fijas en su posición histórica.",
                    "Aceptar");
            }
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

            bool confirmar = true;
            if (Shell.Current is not null)
            {
                string pregunta = resultado.ModificaOrdenActual
                    ? "¿Desea aplicar la nueva secuencia optimizada a su hoja de ruta local?"
                    : "La secuencia actual ya coincide con el recorrido óptimo 2-Opt. ¿Desea reafirmar y mostrar el resumen comparativo?";

                confirmar = await Shell.Current.DisplayAlertAsync(
                    "Optimización de Orden de Paradas",
                    $"{resultado.ResumenFormateado}\n\n{pregunta}",
                    "Aplicar Optimización",
                    "Cancelar");
            }

            if (confirmar)
            {
                await AplicarSecuenciaParadasAsync(resultado.SecuenciaOptimizada);

                ResumenCriterioEstandarTexto = $"{resultado.DistanciaBaseKm:F1} km (~{resultado.TiempoEstimadoBaseMinutos} min)";
                ResumenRutaOptimizadaTexto = $"{resultado.DistanciaOptimizadaKm:F1} km (~{resultado.TiempoEstimadoOptimizadoMinutos} min)";
                ResumenAhorroRecorridoTexto = $"-{resultado.PorcentajeAhorro:F1}% (-{resultado.AhorroKm:F1} km)";
                ResumenTiempoReducidoTexto = $"-{resultado.AhorroMinutos} minutos";
                ResumenSecuenciaAplicadaTexto = string.Join(" -> ", resultado.SecuenciaOptimizada.Select(p => p.CodigoSeguimiento));
                MostrarResumenOptimizacion = true;
            }
        }
        finally
        {
            IsBusy = false;
        }
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

            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Orden de Ruta Restaurado",
                    "Se ha restaurado el orden inicial bajo el Criterio Estándar de Despacho y actualizado en SQLite local.",
                    "Aceptar");
            }
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
