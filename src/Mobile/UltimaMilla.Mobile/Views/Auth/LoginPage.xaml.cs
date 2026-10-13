using UltimaMilla.Mobile.ViewModels.Auth;

namespace UltimaMilla.Mobile.Views.Auth;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
