using UltimaMilla.Mobile.ViewModels.Entrega;

namespace UltimaMilla.Mobile.Views.Entrega;

public partial class IntentoFallidoPage : ContentPage
{
    public IntentoFallidoPage(IntentoFallidoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
