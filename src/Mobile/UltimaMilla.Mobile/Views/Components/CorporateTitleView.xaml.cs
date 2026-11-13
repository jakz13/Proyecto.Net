namespace UltimaMilla.Mobile.Views.Components;

/// <summary>
/// Cabecera corporativa reutilizable para las páginas principales del Shell.
/// Cada página instancia su propia vista para evitar conflictos de jerarquía nativa en Android.
/// </summary>
public partial class CorporateTitleView : ContentView
{
    public CorporateTitleView()
    {
        InitializeComponent();
        BindingContext = Shell.Current as AppShell;
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (BindingContext is null && Shell.Current is AppShell appShell)
        {
            BindingContext = appShell;
        }
    }

    private async void OnSalirClicked(object? sender, EventArgs e)
    {
        if (Shell.Current is AppShell appShell)
        {
            await appShell.EjecutarCerrarSesionAsync();
        }
    }
}

