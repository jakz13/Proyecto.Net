using UltimaMilla.Mobile.ViewModels.Ruta;

namespace UltimaMilla.Mobile.Views.Ruta;

public partial class RutaDiaPage : ContentPage
{
    private readonly RutaDiaViewModel _viewModel;

    public RutaDiaPage(RutaDiaViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.InicializarAsync();

        // Animación de llenado suave del ProgressBar nativo
        if (RutaProgressBar is not null)
        {
            RutaProgressBar.Progress = 0;
            await RutaProgressBar.ProgressTo(_viewModel.ProgresoRuta, 750, Easing.CubicOut);
        }
    }
}
