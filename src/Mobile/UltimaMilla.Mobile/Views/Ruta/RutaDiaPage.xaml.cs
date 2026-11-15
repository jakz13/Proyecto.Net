using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Mobile.Services.Auth;
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

        try
        {
            var authService = Handler?.MauiContext?.Services.GetService<IAuthService>();
            if (authService is not null && !await authService.ValidarSesionVigenteEnServidorAsync())
            {
                return;
            }

            await _viewModel.InicializarAsync();

            // Animación de llenado suave del ProgressBar nativo
            if (RutaProgressBar is not null)
            {
                RutaProgressBar.Progress = 0;
                await RutaProgressBar.ProgressTo(_viewModel.ProgresoRuta, 750, Easing.CubicOut);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RutaDiaPage] Excepción capturada en OnAppearing: {ex.Message}");
        }
    }
}
