using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Services.Auth;
using UltimaMilla.Mobile.Services.Notifications;
using UltimaMilla.Mobile.Services.Realtime;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.Services.Theme;
using UltimaMilla.Mobile.Services.Tracking;
using UltimaMilla.Mobile.Views.Deposito;
using UltimaMilla.Mobile.Views.Entrega;
using UltimaMilla.Mobile.Views.Ruta;

namespace UltimaMilla.Mobile;

public partial class AppShell : Shell
{
    private readonly IAuthService? _authService;
    private readonly IOperadorThemeService? _themeService;
    private readonly IGeolocalizacionTrackingService? _trackingService;
    private readonly ILocalDatabaseService? _localDatabaseService;
    private readonly ISyncService? _syncService;
    private readonly INotificacionLocalService? _notificacionService;
    private readonly IRealtimeTrackingService? _realtimeTrackingService;
    private bool _sesionInicialVerificada;

    private string _nombreOperador = "Última Milla";
    public string NombreOperador
    {
        get => _nombreOperador;
        set { if (_nombreOperador != value) { _nombreOperador = value; OnPropertyChanged(); } }
    }

    private string _inicialesOperador = "UM";
    public string InicialesOperador
    {
        get => _inicialesOperador;
        set { if (_inicialesOperador != value) { _inicialesOperador = value; OnPropertyChanged(); } }
    }

    private ImageSource? _logoSource;
    public ImageSource? LogoSource
    {
        get => _logoSource;
        set
        {
            _logoSource = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TieneLogoOperador));
            OnPropertyChanged(nameof(MostrarInicialesOperador));
        }
    }

    public bool TieneLogoOperador => LogoSource is not null;
    public bool MostrarInicialesOperador => LogoSource is null;

    private string _rolActivoTexto = "REPARTIDOR";
    public string RolActivoTexto
    {
        get => _rolActivoTexto;
        set { if (_rolActivoTexto != value) { _rolActivoTexto = value; OnPropertyChanged(); } }
    }

    private string _estadoConexionTexto = "En Línea";
    public string EstadoConexionTexto
    {
        get => _estadoConexionTexto;
        set { if (_estadoConexionTexto != value) { _estadoConexionTexto = value; OnPropertyChanged(); } }
    }

    private Color _conexionDotColor = Color.FromArgb("#16A34A");
    public Color ConexionDotColor
    {
        get => _conexionDotColor;
        set { _conexionDotColor = value; OnPropertyChanged(); }
    }

    private Color _conexionBadgeBg = Color.FromArgb("#064E3B");
    public Color ConexionBadgeBg
    {
        get => _conexionBadgeBg;
        set { _conexionBadgeBg = value; OnPropertyChanged(); }
    }

    private Color _conexionBadgeBorder = Color.FromArgb("#059669");
    public Color ConexionBadgeBorder
    {
        get => _conexionBadgeBorder;
        set { _conexionBadgeBorder = value; OnPropertyChanged(); }
    }

    private Color _conexionBadgeText = Color.FromArgb("#A7F3D0");
    public Color ConexionBadgeText
    {
        get => _conexionBadgeText;
        set { _conexionBadgeText = value; OnPropertyChanged(); }
    }

    public AppShell() : this(null, null, null, null, null, null, null)
    {
    }

    public AppShell(
        IAuthService? authService,
        IOperadorThemeService? themeService = null,
        IGeolocalizacionTrackingService? trackingService = null,
        ILocalDatabaseService? localDatabaseService = null,
        ISyncService? syncService = null,
        INotificacionLocalService? notificacionService = null,
        IRealtimeTrackingService? realtimeTrackingService = null)
    {
        InitializeComponent();
        BindingContext = this;

        _authService = authService;
        _themeService = themeService;
        _trackingService = trackingService;
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;
        _notificacionService = notificacionService;
        _realtimeTrackingService = realtimeTrackingService;

        if (_themeService != null)
        {
            ActualizarDatosBranding(_themeService.IdentidadActual);
            _themeService.IdentidadVisualCambiada += (_, identidad) => ActualizarDatosBranding(identidad);
        }

        if (_syncService != null)
        {
            ActualizarIndicadorConexion(_syncService.IsOnline);
            _syncService.OnSyncStateChanged += (_, args) => ActualizarIndicadorConexion(args.IsOnline);
        }

        // Registro de rutas secundarias para navegación jerárquica (GoToAsync)
        Routing.RegisterRoute(nameof(DetalleParadaPage), typeof(DetalleParadaPage));
        Routing.RegisterRoute(nameof(RegistroEntregaPage), typeof(RegistroEntregaPage));
        Routing.RegisterRoute(nameof(RegistroRetiroPage), typeof(RegistroRetiroPage));
        Routing.RegisterRoute(nameof(IntentoFallidoPage), typeof(IntentoFallidoPage));
        Routing.RegisterRoute(nameof(RecepcionDepositoPage), typeof(RecepcionDepositoPage));
    }

    private void ActualizarDatosBranding(OperadorIdentidadVisualModel? identidad)
    {
        if (identidad is null)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var nombre = !string.IsNullOrWhiteSpace(identidad.NombreComercial) ? identidad.NombreComercial : "Última Milla";
            Title = nombre;
            NombreOperador = nombre;
            InicialesOperador = identidad.Iniciales;
            LogoSource = identidad.LogoSource;
        });
    }

    private void ActualizarIndicadorConexion(bool enLinea)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (enLinea)
            {
                EstadoConexionTexto = "En Línea";
                ConexionDotColor = Color.FromArgb("#34D399");
                ConexionBadgeBg = Color.FromArgb("#064E3B");
                ConexionBadgeBorder = Color.FromArgb("#059669");
                ConexionBadgeText = Color.FromArgb("#D1FAE5");
            }
            else
            {
                EstadoConexionTexto = "Modo Offline";
                ConexionDotColor = Color.FromArgb("#F87171");
                ConexionBadgeBg = Color.FromArgb("#7F1D1D");
                ConexionBadgeBorder = Color.FromArgb("#DC2626");
                ConexionBadgeText = Color.FromArgb("#FEE2E2");
            }
        });
    }

    private async void OnSincronizarRapidoClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_themeService != null)
            {
                await _themeService.SincronizarIdentidadVisualAsync();
            }

            if (_syncService != null && _syncService.IsOnline)
            {
                await _syncService.SincronizarOperacionesPendientesAsync();
                if (RolActivoTexto == "OPERARIO DE DEPÓSITO")
                {
                    await _syncService.ObtenerEnviosAdmitidosDepositoAsync();
                }
                else
                {
                    await _syncService.DescargarYActualizarRutasDelServidorAsync();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Error en sincronización rápida: {ex.Message}");
        }
    }

    private async void OnCerrarSesionClicked(object? sender, EventArgs e)
    {
        await EjecutarCerrarSesionAsync();
    }

    public async Task EjecutarCerrarSesionAsync()
    {
        try
        {
            if (_trackingService is { IsTrackingActive: true })
            {
                await _trackingService.DetenerTrackingAsync();
            }

            if (_realtimeTrackingService != null)
            {
                await _realtimeTrackingService.StopAsync();
            }

            if (_authService != null)
            {
                await _authService.CerrarSesionAsync();
            }
            else
            {
                await GoToAsync("//LoginPage");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Error al cerrar sesión desde cabecera: {ex.Message}");
        }
    }

    public void ConfigurarNavegacionPorRol(string? rol)
    {
        var rolEfectivo = !string.IsNullOrWhiteSpace(rol)
            ? rol
            : Preferences.Default.Get("RolUsuario", _authService?.Perfil ?? string.Empty);

        var esOperario = rolEfectivo.Contains("Operario", StringComparison.OrdinalIgnoreCase) ||
                         rolEfectivo.Contains("Deposito", StringComparison.OrdinalIgnoreCase);

        var esRepartidor = !esOperario && (
            rolEfectivo.Contains("Repartidor", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(rolEfectivo));

        EjecutarEnMainThread(() =>
        {
            try
            {
                RolActivoTexto = esOperario ? "OPERARIO DE DEPÓSITO" : "REPARTIDOR";

                // Habilitar primero la pestaña destino antes de ocultar las restantes para evitar un TabBar vacío en Android.
                if (esOperario)
                {
                    if (TabRecepcionDeposito != null) TabRecepcionDeposito.IsVisible = true;
                    if (TabMiRuta != null) TabMiRuta.IsVisible = false;
                    if (TabCarga != null) TabCarga.IsVisible = false;
                }
                else
                {
                    if (TabMiRuta != null) TabMiRuta.IsVisible = esRepartidor;
                    if (TabCarga != null) TabCarga.IsVisible = esRepartidor;
                    if (TabRecepcionDeposito != null) TabRecepcionDeposito.IsVisible = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppShell] Error al configurar navegación por rol: {ex.Message}");
            }
        });
    }

    public void RestablecerPestanas()
    {
        EjecutarEnMainThread(() =>
        {
            try
            {
                RolActivoTexto = "REPARTIDOR";
                if (TabMiRuta != null) TabMiRuta.IsVisible = true;
                if (TabCarga != null) TabCarga.IsVisible = true;
                if (TabRecepcionDeposito != null) TabRecepcionDeposito.IsVisible = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppShell] Error al restablecer pestañas: {ex.Message}");
            }
        });
    }

    private static void EjecutarEnMainThread(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }

    public void ActualizarPestanasPorPerfil()
    {
        ConfigurarNavegacionPorRol(null);
    }

    public async Task VerificarSesionInicialAsync()
    {
        if (_sesionInicialVerificada || _authService is null)
            return;

        _sesionInicialVerificada = true;

        try
        {
            var autenticado = await _authService.EstaAutenticadoAsync();
            if (autenticado)
            {
                var rol = _authService.Perfil ?? Preferences.Default.Get("RolUsuario", string.Empty);
                ConfigurarNavegacionPorRol(rol);

                if (_themeService != null)
                {
                    _ = _themeService.SincronizarIdentidadVisualAsync();
                }

                if (_notificacionService != null)
                {
                    _ = _notificacionService.SolicitarPermisosNotificacionAsync();
                }

                if (_realtimeTrackingService != null)
                {
                    _ = _realtimeTrackingService.StartAsync();
                }

                var esOperario = rol.Contains("Operario", StringComparison.OrdinalIgnoreCase) ||
                                 rol.Contains("Deposito", StringComparison.OrdinalIgnoreCase);

                if (esOperario)
                {
                    await GoToAsync("//RecepcionDepositoPage");
                }
                else
                {
                    // El rastreo y los permisos nativos se inicializan en RutaDiaViewModel.InicializarAsync()
                    // una vez que la vista principal ya completó la navegación en Android.
                    await GoToAsync("//RutaDiaPage");
                }
            }
            else
            {
                RestablecerPestanas();
                if (CurrentState?.Location?.ToString() != "//LoginPage")
                {
                    await GoToAsync("//LoginPage");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Error al verificar sesión inicial: {ex.Message}");
            try
            {
                if (CurrentState?.Location?.ToString() != "//LoginPage")
                {
                    await GoToAsync("//LoginPage");
                }
            }
            catch
            {
                // Fallback silencioso si ya se encuentra en LoginPage por defecto
            }
        }
    }
}
