using UltimaMilla.Mobile.Controls;
using UltimaMilla.Mobile.ViewModels.Entrega;

namespace UltimaMilla.Mobile.Views.Entrega;

public partial class RegistroRetiroPage : ContentPage
{
    public RegistroRetiroPage() : this(
        Application.Current?.Handler?.MauiContext?.Services.GetService<RegistroRetiroViewModel>()
        ?? new RegistroRetiroViewModel())
    {
    }

    public RegistroRetiroPage(RegistroRetiroViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        FirmaPadConector.Conectar(FirmaGraphicsView, viewModel);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is RegistroRetiroViewModel vm)
        {
            _ = vm.ActualizarUbicacionYFechaAsync();
        }
    }
}
