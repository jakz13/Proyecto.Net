using UltimaMilla.Mobile.Views.Entrega;
using UltimaMilla.Mobile.Views.Ruta;

namespace UltimaMilla.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Registro de rutas secundarias para navegación jerárquica (GoToAsync)
        Routing.RegisterRoute(nameof(DetalleParadaPage), typeof(DetalleParadaPage));
        Routing.RegisterRoute(nameof(RegistroEntregaPage), typeof(RegistroEntregaPage));
        Routing.RegisterRoute(nameof(IntentoFallidoPage), typeof(IntentoFallidoPage));
    }
}
