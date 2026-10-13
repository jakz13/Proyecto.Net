using UltimaMilla.Mobile.Views.Entrega;

namespace UltimaMilla.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Registro de rutas secundarias para navegación jerárquica (GoToAsync)
        Routing.RegisterRoute(nameof(RegistroEntregaPage), typeof(RegistroEntregaPage));
    }
}
