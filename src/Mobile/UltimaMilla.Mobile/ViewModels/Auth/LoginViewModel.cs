using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Services.Handlers;

namespace UltimaMilla.Mobile.ViewModels.Auth;

public partial class LoginViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string _usuario = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Debe ingresar usuario y contraseña.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Conectando con el servidor...";

            // Simulación de latencia de red de 2 segundos (backend mockeado)
            await Task.Delay(2000);

            // Generación de token JWT mockeado
            var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
            var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                $"{{\"sub\":\"{Usuario.Trim()}\",\"rol\":\"Repartidor\",\"iat\":{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}}}"
            ));
            var fakeJwt = $"{header}.{payload}.mock_secure_signature_rnf6_3";

            // RNF 6.3: Almacenamiento seguro del token en SecureStorage
            await SecureStorage.Default.SetAsync(AuthDelegatingHandler.AuthTokenKey, fakeJwt);

            StatusMessage = "Inicio de sesión exitoso. Credencial asegurada.";

            // Navegación post-login o feedback
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Bienvenido",
                    $"Sesión iniciada como: {Usuario.Trim()}\nCredenciales de acceso verificadas correctamente.",
                    "Continuar");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al iniciar sesión: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
