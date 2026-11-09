using UltimaMilla.Mobile.ViewModels.Auth;

namespace UltimaMilla.Mobile.Views.Auth;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is LoginViewModel vm)
        {
            await vm.InicializarAsync();
        }
    }
}
