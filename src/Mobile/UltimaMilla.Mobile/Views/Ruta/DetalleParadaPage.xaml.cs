using UltimaMilla.Mobile.ViewModels.Ruta;

namespace UltimaMilla.Mobile.Views.Ruta;

public partial class DetalleParadaPage : ContentPage
{
    private readonly DetalleParadaViewModel _viewModel;

    public DetalleParadaPage(DetalleParadaViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.IniciarVigilancia();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.DetenerVigilancia();
    }
}
