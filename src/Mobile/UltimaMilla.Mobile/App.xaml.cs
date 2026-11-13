using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Mobile.Responsive;
using UltimaMilla.Mobile.Services.Theme;

namespace UltimaMilla.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOperadorThemeService? _themeService;
    private AppShell? _appShell;

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
