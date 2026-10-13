using UltimaMilla.Mobile.ViewModels.Carga;

namespace UltimaMilla.Mobile.Views.Carga;

public partial class CargaVehiculoPage : ContentPage
{
    public CargaVehiculoPage(CargaVehiculoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
