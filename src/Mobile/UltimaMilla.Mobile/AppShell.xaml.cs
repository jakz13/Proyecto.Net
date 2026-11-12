using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Services.Auth;
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
    private bool _sesionInicialVerificada;

    public AppShell() : this(null, null, null, null)
    {
    }

    public AppShell(
        IAuthService? authService,
        IOperadorThemeService? themeService = null,
        IGeolocalizacionTrackingService? trackingService = null,
        ILocalDatabaseService? localDatabaseService = null)
    {
        InitializeComponent();
        _authService = authService;
        _themeService = themeService;
        _trackingService = trackingService;
        _localDatabaseService = localDatabaseService;

        if (_themeService != null)
        {
            ActualizarTitulo(_themeService.IdentidadActual?.NombreComercial);
            _themeService.IdentidadVisualCambiada += (s, identidad) => ActualizarTitulo(identidad.NombreComercial);
        }

        // Registro de rutas secundarias para navegación jerárquica (GoToAsync)
        Routing.RegisterRoute(nameof(DetalleParadaPage), typeof(DetalleParadaPage));
        Routing.RegisterRoute(nameof(RegistroEntregaPage), typeof(RegistroEntregaPage));
        Routing.RegisterRoute(nameof(RegistroRetiroPage), typeof(RegistroRetiroPage));
        Routing.RegisterRoute(nameof(IntentoFallidoPage), typeof(IntentoFallidoPage));
        Routing.RegisterRoute(nameof(RecepcionDepositoPage), typeof(RecepcionDepositoPage));
    }

    private void ActualizarTitulo(string? nombreOperador)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Title = !string.IsNullOrWhiteSpace(nombreOperador) ? nombreOperador : "Última Milla";
        });
    }

    public void ActualizarPestanasPorPerfil()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var esOperario = _authService?.EsOperarioODeposito ?? false;
                var esAdmin = _authService?.EsAdministrador ?? false;

                // La pestaña de recepción en depósito está disponible para operarios de depósito,
                // administradores o en modo demostración para el ciclo completo.
                if (TabRecepcionDeposito != null)
                {
                    TabRecepcionDeposito.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppShell] Error al actualizar visibilidad de pestañas: {ex.Message}");
            }
        });
    }

    public async Task VerificarSesionInicialAsync()
    {
        if (_sesionInicialVerificada || _authService is null)
            return;

        _sesionInicialVerificada = true;

        try
        {
            ActualizarPestanasPorPerfil();

            var autenticado = await _authService.EstaAutenticadoAsync();
            if (autenticado)
            {
                if (_trackingService != null && _authService.RepartidorId is { } repId && repId != Guid.Empty)
                {
                    var rutaLocal = _localDatabaseService != null ? await _localDatabaseService.ObtenerRutaActivaLocalAsync() : null;
                    var rId = rutaLocal?.Ruta?.Id ?? Guid.Empty;
                    _ = _trackingService.IniciarTrackingAsync(rId, repId);
                }

                if (_authService.EsOperarioODeposito && !string.Equals(_authService.Perfil, "Repartidor", StringComparison.OrdinalIgnoreCase))
                {
                    await GoToAsync("//RecepcionDepositoPage");
                }
                else
                {
                    await GoToAsync("//RutaDiaPage");
                }
            }
            else if (CurrentState?.Location?.ToString() != "//LoginPage")
            {
                await GoToAsync("//LoginPage");
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
