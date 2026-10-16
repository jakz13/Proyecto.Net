using UltimaMilla.Mobile.ViewModels.Ruta;

namespace UltimaMilla.Mobile.Views.Ruta;

public partial class DetalleParadaPage : ContentPage
{
    public DetalleParadaPage(DetalleParadaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
