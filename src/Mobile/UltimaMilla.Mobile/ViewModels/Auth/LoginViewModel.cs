using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Configuration;
using UltimaMilla.Mobile.Services.Auth;
using UltimaMilla.Mobile.Services.Sync;

namespace UltimaMilla.Mobile.ViewModels.Auth;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService? _authService;
    private readonly ISyncService? _syncService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string _usuario = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _servidorActivo = ApiEndpoints.BaseUrl;

    [ObservableProperty]
    private string _tipoServidorTexto = ApiEndpoints.EsServidorNube ? "Nube" : "Local";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _statusMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public LoginViewModel() : this(null, null)
    {
    }

    public LoginViewModel(IAuthService? authService, ISyncService? syncService = null)
    {
        _authService = authService;
        _syncService = syncService;
    }

    [RelayCommand]
    private void AlternarServidor()
    {
        if (ApiEndpoints.EsServidorNube)
        {
            ApiEndpoints.UsarServidorLocal();
        }
        else
        {
            ApiEndpoints.UsarServidorNube();
        }

        ServidorActivo = ApiEndpoints.BaseUrl;
        TipoServidorTexto = ApiEndpoints.EsServidorNube ? "Nube" : "Local";
        StatusMessage = $"Servidor configurado: {TipoServidorTexto} ({ServidorActivo})";
    }

    [RelayCommand]
    private void CargarCredencialesDemo()
    {
        ErrorMessage = null;
        Usuario = AuthService.DemoRepartidorEmail;
        Password = AuthService.DemoRepartidorPassword;
        StatusMessage = "Credenciales de demostración cargadas.";
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Debe ingresar su usuario/email y contraseña.";
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Datos Requeridos",
                    ErrorMessage,
                    "Aceptar");
            }
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Verificando credenciales con el servidor...";

            if (_authService is null)
            {
                ErrorMessage = "El servicio de autenticación no se encuentra disponible.";
                return;
            }

            var resultado = await _authService.LoginAsync(Usuario.Trim(), Password);

            if (!resultado.Exitoso)
            {
                StatusMessage = null;
                ErrorMessage = resultado.Mensaje;

                if (Shell.Current is not null)
                {
                    await Shell.Current.DisplayAlertAsync(
                        "Error de Autenticación",
                        resultado.Mensaje,
                        "Aceptar");
                }
                return;
            }

            StatusMessage = "Descargando hoja de ruta asignada...";

            if (_syncService is not null)
            {
                await _syncService.DescargarRutaDelDiaAsync();
            }

            StatusMessage = "Sesión iniciada correctamente.";

            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("//RutaDiaPage");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = null;
            ErrorMessage = $"Error inesperado al iniciar sesión: {ex.Message}";

            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Error de Inicio de Sesión",
                    ErrorMessage,
                    "Aceptar");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
