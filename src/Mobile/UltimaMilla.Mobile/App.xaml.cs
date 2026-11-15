using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Mobile.Responsive;
using UltimaMilla.Mobile.Services.Auth;
using UltimaMilla.Mobile.Services.Realtime;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.Services.Theme;

namespace UltimaMilla.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOperadorThemeService? _themeService;
    private AppShell? _appShell;
    private DateTimeOffset _ultimaValidacionResumeUtc = DateTimeOffset.MinValue;

    public App(IServiceProvider serviceProvider, IOperadorThemeService? themeService = null)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _themeService = themeService;

        // Cargar identidad visual guardada en preferencias antes de construir AppShell
        // para garantizar que Colors/Styles y recursos dinámicos existan desde el arranque.
        _themeService?.CargarDesdePreferencias();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            _appShell ??= _serviceProvider.GetRequiredService<AppShell>();
            var window = new Window(_appShell);

            AplicarTokensSegunDisplay();
            window.SizeChanged += (_, _) => TokensResponsive.Aplicar(this, window.Width);
            window.Resumed += (_, _) => _ = ValidarSesionAlReanudarAsync();
            window.Created += async (_, _) =>
            {
                try
                {
                    if (_appShell is not null)
                    {
                        await _appShell.VerificarSesionInicialAsync();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[App] Excepción en window.Created: {ex}");
                }
            };

            return window;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Error crítico en CreateWindow: {ex}");
            return CrearVentanaErrorRecuperacion(ex);
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        _ = ValidarSesionAlReanudarAsync();
    }

    private async Task ValidarSesionAlReanudarAsync()
    {
        var ahora = DateTimeOffset.UtcNow;
        if ((ahora - _ultimaValidacionResumeUtc) < TimeSpan.FromSeconds(2))
            return;

        _ultimaValidacionResumeUtc = ahora;

        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return;

            var authService = _serviceProvider.GetService<IAuthService>();
            if (authService is not null)
            {
                var vigente = await authService.ValidarSesionVigenteEnServidorAsync();
                if (!vigente || string.IsNullOrWhiteSpace(authService.AccessToken))
                    return;

                var realtime = _serviceProvider.GetService<IRealtimeTrackingService>();
                if (realtime is not null)
                {
                    _ = realtime.StartAsync();
                }

                var themeService = _themeService ?? _serviceProvider.GetService<IOperadorThemeService>();
                if (themeService is not null)
                {
                    await themeService.SincronizarIdentidadVisualAsync();
                }

                var syncService = _serviceProvider.GetService<ISyncService>();
                if (syncService is not null && syncService.IsOnline)
                {
                    if (await syncService.ObtenerCantidadPendientesAsync() > 0)
                        await syncService.SincronizarOperacionesPendientesAsync();

                    if (!authService.EsOperarioODeposito)
                        await syncService.DescargarYActualizarRutasDelServidorAsync();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Error no bloqueante al validar sesión en OnResume: {ex.Message}");
        }
    }

    private static Window CrearVentanaErrorRecuperacion(Exception ex)
    {
        var detalle = $"{ex.GetType().Name}: {ex.Message}";
        var page = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#F8FAFC"),
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = 24,
                    Spacing = 12,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label
                        {
                            Text = "No se pudo inicializar la pantalla principal",
                            FontSize = 18,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#0F172A")
                        },
                        new Label
                        {
                            Text = detalle,
                            FontSize = 13,
                            TextColor = Color.FromArgb("#B91C1C")
                        }
                    }
                }
            }
        };
        return new Window(page);
    }

    /// <summary>
    /// En teléfonos el ancho de ventana puede no estar disponible al crearla; se usa el ancho del
    /// display (en dp) para que la primera pantalla ya se dibuje con los tokens correctos.
    /// </summary>
    private void AplicarTokensSegunDisplay()
    {
        if (DeviceInfo.Current.Idiom != DeviceIdiom.Phone)
            return;

        var display = DeviceDisplay.Current.MainDisplayInfo;
        if (display.Density > 0)
            TokensResponsive.Aplicar(this, display.Width / display.Density);
    }
}
