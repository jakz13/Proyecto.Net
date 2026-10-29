using UltimaMilla.Mobile.Responsive;

namespace UltimaMilla.Mobile;

public partial class App : Application
{
    private readonly AppShell _appShell;

    public App(AppShell appShell)
    {
        InitializeComponent();
        _appShell = appShell;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_appShell);

        AplicarTokensSegunDisplay();
        window.SizeChanged += (_, _) => TokensResponsive.Aplicar(this, window.Width);

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


