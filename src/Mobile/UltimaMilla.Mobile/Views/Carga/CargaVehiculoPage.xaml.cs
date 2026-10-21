using UltimaMilla.Mobile.ViewModels.Carga;

namespace UltimaMilla.Mobile.Views.Carga;

public partial class CargaVehiculoPage : ContentPage
{
    private readonly CargaVehiculoViewModel _viewModel;

    public CargaVehiculoPage(CargaVehiculoViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InicializarAsync();
    }
}
