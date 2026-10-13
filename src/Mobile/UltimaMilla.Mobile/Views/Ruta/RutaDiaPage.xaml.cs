using UltimaMilla.Mobile.ViewModels.Ruta;

namespace UltimaMilla.Mobile.Views.Ruta;

public partial class RutaDiaPage : ContentPage
{
    public RutaDiaPage(RutaDiaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
