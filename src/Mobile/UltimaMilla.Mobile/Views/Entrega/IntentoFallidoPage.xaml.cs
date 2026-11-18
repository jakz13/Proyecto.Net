using UltimaMilla.Mobile.ViewModels.Entrega;

namespace UltimaMilla.Mobile.Views.Entrega;

public partial class IntentoFallidoPage : ContentPage
{
    private readonly IntentoFallidoViewModel _viewModel;

    public IntentoFallidoPage(IntentoFallidoViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CargarMotivosAsync();
        _viewModel.IniciarVigilanciaDeRuta();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.DetenerVigilanciaDeRuta();
    }
}
