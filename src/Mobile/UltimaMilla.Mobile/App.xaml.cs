using UltimaMilla.Mobile.Responsive;
using UltimaMilla.Mobile.Services.Theme;

namespace UltimaMilla.Mobile;

public partial class App : Application
{
    private readonly AppShell _appShell;
    private readonly IOperadorThemeService? _themeService;

    public App(AppShell appShell, IOperadorThemeService? themeService = null)
    {
        InitializeComponent();
        _appShell = appShell;
        _themeService = themeService;

        // Cargar identidad visual guardada en preferencias (marca blanca offline)
        _themeService?.CargarDesdePreferencias();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_appShell);

        AplicarTokensSegunDisplay();
        window.SizeChanged += (_, _) => TokensResponsive.Aplicar(this, window.Width);
        window.Created += async (_, _) =>
        {
            try
            {
                await _appShell.VerificarSesionInicialAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Excepción en window.Created: {ex.Message}");
            }
        };

        return window;
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
