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
        try
        {
            if (BindingContext is LoginViewModel vm)
            {
                await vm.InicializarAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoginPage] Excepción capturada en OnAppearing: {ex.Message}");
        }
    }
}
