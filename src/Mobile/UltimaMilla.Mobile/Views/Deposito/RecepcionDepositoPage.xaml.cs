using UltimaMilla.Mobile.ViewModels.Deposito;

namespace UltimaMilla.Mobile.Views.Deposito;

public partial class RecepcionDepositoPage : ContentPage
{
    private readonly RecepcionDepositoViewModel _viewModel;

    public RecepcionDepositoPage(RecepcionDepositoViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_viewModel.EscaneandoConCamara)
        {
            await _viewModel.InicializarAsync();
        }
    }
}

