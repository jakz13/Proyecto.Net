using UltimaMilla.Mobile.ViewModels.Carga;

namespace UltimaMilla.Mobile.Views.Carga;

public partial class CargaVehiculoPage : ContentPage
{
    private readonly CargaVehiculoViewModel _viewModel;

    public CargaVehiculoPage() : this(
        Application.Current?.Handler?.MauiContext?.Services.GetService<CargaVehiculoViewModel>() 
        ?? new CargaVehiculoViewModel())
    {
    }

    public CargaVehiculoPage(CargaVehiculoViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            if (!_viewModel.EscaneandoConCamara)
            {
                await _viewModel.InicializarAsync();
            }
            _viewModel.IniciarVigilanciaDespacho();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CargaVehiculoPage] Excepción al inicializar: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        _viewModel.DetenerVigilanciaDespacho();
        base.OnDisappearing();
    }
}
