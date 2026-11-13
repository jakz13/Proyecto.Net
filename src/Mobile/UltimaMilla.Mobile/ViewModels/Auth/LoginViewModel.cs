using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Configuration;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Services.Auth;
using UltimaMilla.Mobile.Services.Notifications;
using UltimaMilla.Mobile.Services.Realtime;
using UltimaMilla.Mobile.Services.Security;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.Services.Theme;
using UltimaMilla.Mobile.Services.Tracking;

namespace UltimaMilla.Mobile.ViewModels.Auth;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService? _authService;
    private readonly ISyncService? _syncService;
    private readonly IBiometricAuthService? _biometricAuthService;
    private readonly IOperadorThemeService? _themeService;
    private readonly IGeolocalizacionTrackingService? _trackingService;
    private readonly ILocalDatabaseService? _localDatabaseService;
    private readonly INotificacionLocalService? _notificacionService;
    private readonly IRealtimeTrackingService? _realtimeTrackingService;

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
    private string _nombreOperador = "Última Milla";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneLogoUrl))]
    [NotifyPropertyChangedFor(nameof(MostrarIniciales))]
    private string? _logoOperadorUrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneLogoUrl))]
    [NotifyPropertyChangedFor(nameof(MostrarIniciales))]
    private ImageSource? _logoSource;

    public bool TieneLogoUrl => LogoSource is not null;

    public bool MostrarIniciales => LogoSource is null;

    [ObservableProperty]
    private string _inicialesOperador = "UM";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _statusMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public LoginViewModel() : this(null, null, null, null, null, null, null, null)
    {
    }

    public LoginViewModel(
        IAuthService? authService,
        ISyncService? syncService = null,
        IBiometricAuthService? biometricAuthService = null,
        IOperadorThemeService? themeService = null,
        IGeolocalizacionTrackingService? trackingService = null,
        ILocalDatabaseService? localDatabaseService = null,
        INotificacionLocalService? notificacionService = null,
        IRealtimeTrackingService? realtimeTrackingService = null)
    {
        _authService = authService;
        _syncService = syncService;
        _biometricAuthService = biometricAuthService;
        _themeService = themeService;
        _trackingService = trackingService;
        _localDatabaseService = localDatabaseService;
        _notificacionService = notificacionService;
        _realtimeTrackingService = realtimeTrackingService;

        if (_themeService != null)
        {
            ActualizarIdentidadVisual(_themeService.IdentidadActual);
            _themeService.IdentidadVisualCambiada += (s, identidad) => ActualizarIdentidadVisual(identidad);
        }
    }

    private void ActualizarIdentidadVisual(OperadorIdentidadVisualModel? identidad)
    {
        if (identidad == null) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            NombreOperador = !string.IsNullOrWhiteSpace(identidad.NombreComercial) ? identidad.NombreComercial : "Última Milla";
            LogoOperadorUrl = identidad.LogoUrl;
            LogoSource = identidad.LogoSource;
            InicialesOperador = identidad.Iniciales;
        });
    }

    public async Task InicializarAsync()
    {
        try
        {
            var rutaCrash = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ultimo_crash.log");
            if (File.Exists(rutaCrash))
            {
                var contenido = await File.ReadAllTextAsync(rutaCrash);
                File.Delete(rutaCrash);
                if (!string.IsNullOrWhiteSpace(contenido))
                {
                    var lineas = contenido.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(6);
                    ErrorMessage = $"Diagnóstico de cierre previo:\n{string.Join('\n', lineas)}";
                }
            }
        }
        catch
        {
            // Ignorar fallos al leer el diagnóstico local
        }

        _themeService?.CargarDesdePreferencias();
        if (_themeService != null)
        {
            ActualizarIdentidadVisual(_themeService.IdentidadActual);
        }

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

            if (_notificacionService is not null)
            {
                await _notificacionService.SolicitarPermisosNotificacionAsync();
            }

            if (_realtimeTrackingService is not null)
            {
                _ = _realtimeTrackingService.StartAsync();
            }

            StatusMessage = "Sesión iniciada correctamente.";

            var rol = resultado.Perfil ?? _authService?.Perfil ?? Preferences.Default.Get("RolUsuario", string.Empty);

            if (Shell.Current is AppShell appShell)
            {
                appShell.ConfigurarNavegacionPorRol(rol);
            }

            var esOperario = rol.Contains("Operario", StringComparison.OrdinalIgnoreCase) ||
                             rol.Contains("Deposito", StringComparison.OrdinalIgnoreCase);

            if (Shell.Current is not null)
            {
                if (esOperario)
                {
                    await Shell.Current.GoToAsync("//RecepcionDepositoPage");
                }
                else
                {
                    await Shell.Current.GoToAsync("//RutaDiaPage");
                }
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

                if (_notificacionService is not null)
                {
                    await _notificacionService.SolicitarPermisosNotificacionAsync();
                }

                if (_realtimeTrackingService is not null)
                {
                    _ = _realtimeTrackingService.StartAsync();
                }

                var rol = _authService.Perfil ?? Preferences.Default.Get("RolUsuario", string.Empty);

                if (Shell.Current is AppShell appShellBio)
                {
                    appShellBio.ConfigurarNavegacionPorRol(rol);
                }

                var esOperario = rol.Contains("Operario", StringComparison.OrdinalIgnoreCase) ||
                                 rol.Contains("Deposito", StringComparison.OrdinalIgnoreCase);

                if (Shell.Current is not null)
                {
                    if (esOperario)
                    {
                        await Shell.Current.GoToAsync("//RecepcionDepositoPage");
                    }
                    else
                    {
                        await Shell.Current.GoToAsync("//RutaDiaPage");
                    }
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
