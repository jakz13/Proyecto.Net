using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Configuration;
using UltimaMilla.Mobile.Services.Auth;
using UltimaMilla.Mobile.Services.Security;
using UltimaMilla.Mobile.Services.Sync;

namespace UltimaMilla.Mobile.ViewModels.Auth;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService? _authService;
    private readonly ISyncService? _syncService;
    private readonly IBiometricAuthService? _biometricAuthService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private bool _puedeUsarBiometria;

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

    public LoginViewModel() : this(null, null, null)
    {
    }

    public LoginViewModel(
        IAuthService? authService,
        ISyncService? syncService = null,
        IBiometricAuthService? biometricAuthService = null)
    {
        _authService = authService;
        _syncService = syncService;
        _biometricAuthService = biometricAuthService;
    }

    public async Task InicializarAsync()
    {
        if (_biometricAuthService is not null)
        {
            var soporta = await _biometricAuthService.DispositivoSoportaBiometriaAsync();
            var tieneSesion = _authService is not null && await _authService.EstaAutenticadoAsync();
            PuedeUsarBiometria = soporta || tieneSesion;
        }
        else
        {
            PuedeUsarBiometria = false;
        }
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

    [RelayCommand]
    private async Task LoginConBiometriaAsync()
    {
        if (IsBusy)
            return;

        ErrorMessage = null;
        StatusMessage = null;

        if (_biometricAuthService is null)
        {
            ErrorMessage = "Servicio de autenticación biométrica no disponible.";
            return;
        }

        var autenticado = await _biometricAuthService.ValidarIdentidadAsync(
            "Desbloqueo Biométrico",
            "Verifique su huella digital o PIN del dispositivo para ingresar al sistema.");

        if (!autenticado)
        {
            StatusMessage = "Autenticación biométrica no completada.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Iniciando sesión con credenciales seguras...";

            if (_authService is not null && await _authService.EstaAutenticadoAsync())
            {
                StatusMessage = "Sesión validada. Descargando hoja de ruta...";
                if (_syncService is not null)
                {
                    await _syncService.DescargarRutaDelDiaAsync();
                }

                if (Shell.Current is not null)
                {
                    await Shell.Current.GoToAsync("//RutaDiaPage");
                }
                return;
            }

            // Sin una sesión guardada no hay con qué reanudar: se piden las credenciales
            StatusMessage = null;
            ErrorMessage = "No hay una sesión guardada en este dispositivo. Ingrese su usuario y contraseña.";
        }
        catch (Exception ex)
        {
            StatusMessage = null;
            ErrorMessage = $"Error al reanudar sesión: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
