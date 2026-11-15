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
using UltimaMilla.Mobile.Services.Updates;

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
    private readonly IAppUpdateService? _appUpdateService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarCorreoBiometriaGuardado))]
    private bool _puedeUsarBiometria;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarCorreoBiometriaGuardado))]
    private string? _correoBiometriaGuardado;

    public bool MostrarCorreoBiometriaGuardado =>
        PuedeUsarBiometria && !string.IsNullOrWhiteSpace(CorreoBiometriaGuardado);

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

    // =========================================================================
    // AUTO-ACTUALIZADOR IN-APP (OTA)
    // =========================================================================
    [ObservableProperty]
    private bool _mostrarModalActualizacion;

    [ObservableProperty]
    private string _versionInstaladaTexto = AppInfo.Current.VersionString;

    [ObservableProperty]
    private string _versionNuevaTexto = AppInfo.Current.VersionString;

    [ObservableProperty]
    private string _notasActualizacionTexto = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoDescargandoActualizacion))]
    private bool _descargandoActualizacion;

    public bool NoDescargandoActualizacion => !DescargandoActualizacion;

    [ObservableProperty]
    private double _progresoDescargaActualizacion;

    [ObservableProperty]
    private string _porcentajeDescargaTexto = "0%";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeEstadoActualizacion))]
    private string? _mensajeEstadoActualizacion;

    public bool TieneMensajeEstadoActualizacion => !string.IsNullOrWhiteSpace(MensajeEstadoActualizacion);

    public LoginViewModel() : this(null, null, null, null, null, null, null, null, null)
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
        IRealtimeTrackingService? realtimeTrackingService = null,
        IAppUpdateService? appUpdateService = null)
    {
        _authService = authService;
        _syncService = syncService;
        _biometricAuthService = biometricAuthService;
        _themeService = themeService;
        _trackingService = trackingService;
        _localDatabaseService = localDatabaseService;
        _notificacionService = notificacionService;
        _realtimeTrackingService = realtimeTrackingService;
        _appUpdateService = appUpdateService;

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

        var tieneSesionBio = _authService is not null && await _authService.TieneSesionBiometricaGuardadaAsync();
        if (_biometricAuthService is not null)
        {
            var soporta = await _biometricAuthService.DispositivoSoportaBiometriaAsync();
            PuedeUsarBiometria = soporta || tieneSesionBio;
        }
        else
        {
            PuedeUsarBiometria = false;
        }

        if (_authService is not null && tieneSesionBio)
        {
            var emailGuardado = await _authService.ObtenerEmailBiometricoGuardadoAsync();
            CorreoBiometriaGuardado = string.IsNullOrWhiteSpace(emailGuardado) ? null : emailGuardado.Trim();
            if (string.IsNullOrWhiteSpace(Usuario) && !string.IsNullOrWhiteSpace(CorreoBiometriaGuardado))
            {
                Usuario = CorreoBiometriaGuardado;
            }
        }
        else
        {
            CorreoBiometriaGuardado = null;
        }

        _ = VerificarActualizacionSilenciosaAsync();
    }

    private async Task VerificarActualizacionSilenciosaAsync()
    {
        if (_appUpdateService is null || _appUpdateService.ActualizacionPospuestaEnSesion)
        {
            return;
        }

        try
        {
            var resultado = await _appUpdateService.VerificarActualizacionAsync();
            if (resultado.HayActualizacionDisponible && !_appUpdateService.ActualizacionPospuestaEnSesion)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    VersionInstaladaTexto = resultado.VersionInstaladaTexto;
                    VersionNuevaTexto = resultado.VersionRemotaTexto;
                    NotasActualizacionTexto = resultado.Notas;
                    ProgresoDescargaActualizacion = 0.0;
                    PorcentajeDescargaTexto = "0%";
                    MensajeEstadoActualizacion = null;
                    DescargandoActualizacion = false;
                    MostrarModalActualizacion = true;
                });
            }
        }
        catch
        {
            // No interrumpir el inicio de sesión si no hay respuesta del servidor de actualizaciones
        }
    }

    [RelayCommand]
    private async Task DescargarEInstalarActualizacionAsync()
    {
        if (_appUpdateService is null || DescargandoActualizacion)
        {
            return;
        }

        try
        {
            DescargandoActualizacion = true;
            MensajeEstadoActualizacion = "Descargando paquete de actualización...";
            ProgresoDescargaActualizacion = 0.0;
            PorcentajeDescargaTexto = "0%";

            var progreso = new Progress<double>(valor =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ProgresoDescargaActualizacion = Math.Clamp(valor, 0.0, 1.0);
                    PorcentajeDescargaTexto = $"{Math.Round(ProgresoDescargaActualizacion * 100)}%";
                });
            });

            var ok = await _appUpdateService.DescargarEInstalarAsync(progreso);
            if (!ok)
            {
                MensajeEstadoActualizacion = _appUpdateService.UltimoErrorDescarga
                    ?? "No fue posible descargar o iniciar el instalador. Verifique su conexión e intente nuevamente.";
            }
            else
            {
                MensajeEstadoActualizacion = "Integridad SHA-256 verificada. Instalador iniciado correctamente.";
            }
        }
        finally
        {
            DescargandoActualizacion = false;
        }
    }

    [RelayCommand]
    private void PosponerActualizacion()
    {
        if (_appUpdateService is not null)
        {
            _appUpdateService.ActualizacionPospuestaEnSesion = true;
        }

        MostrarModalActualizacion = false;
    }

    [RelayCommand]
    private Task IniciarSesionAsync() => LoginAsync();

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Debe ingresar su correo electrónico y contraseña.";
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

            var resultado = await _authService.IniciarSesionAsync(Usuario.Trim(), Password);

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

            PuedeUsarBiometria = true;
            CorreoBiometriaGuardado = Usuario.Trim();
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
    private Task LoginConBiometriaAsync() => IngresarConBiometriaAsync();

    [RelayCommand]
    private async Task IngresarConBiometriaAsync()
    {
        if (IsBusy)
            return;

        ErrorMessage = null;
        StatusMessage = null;

        if (_authService is null)
        {
            ErrorMessage = "El servicio de autenticación no se encuentra disponible.";
            return;
        }

        // 1. Verificar primero en SecureStorage si existe vínculo biométrico habilitado y credencial/token guardado
        var tieneSesionBiometrica = await _authService.TieneSesionBiometricaGuardadaAsync();
        if (!tieneSesionBiometrica)
        {
            CorreoBiometriaGuardado = null;
            ErrorMessage = "No hay una sesión guardada en este dispositivo. Ingresa primero con tu correo electrónico y contraseña para vincular tu huella.";
            return;
        }

        // 1b. Validar estrictamente que el correo ingresado coincida con el vínculo biométrico guardado
        var correoGuardado = await _authService.ObtenerEmailBiometricoGuardadoAsync();
        CorreoBiometriaGuardado = string.IsNullOrWhiteSpace(correoGuardado) ? null : correoGuardado.Trim();
        var correoIngresado = Usuario?.Trim();

        if (!string.IsNullOrWhiteSpace(correoIngresado)
            && !string.IsNullOrWhiteSpace(correoGuardado)
            && !string.Equals(correoIngresado, correoGuardado.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = $"La biometría de este dispositivo está vinculada a {correoGuardado.Trim()}. Ingresa con tu contraseña para iniciar sesión con esta cuenta.";
            return;
        }

        if (_biometricAuthService is null)
        {
            ErrorMessage = "El servicio de autenticación biométrica no se encuentra disponible.";
            return;
        }

        // 2. Disparar el prompt nativo de huella o rostro
        var autenticado = await _biometricAuthService.AutenticarAsync(
            "Verifica tu identidad para ingresar a Última Milla");

        if (!autenticado)
        {
            StatusMessage = "Autenticación biométrica cancelada o no completada.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Restaurando sesión segura con biometría...";

            // 3. Restaurar la sesión activa (validando/renovando el JWT si hay conexión, o permitiendo acceso offline con SQLCipher)
            var resultado = await _authService.RestaurarSesionBiometricaAsync();
            if (!resultado.Exitoso)
            {
                StatusMessage = null;
                ErrorMessage = resultado.Mensaje;
                if (!await _authService.TieneSesionBiometricaGuardadaAsync())
                {
                    CorreoBiometriaGuardado = null;
                }
                return;
            }

            StatusMessage = "Sesión validada. Sincronizando datos operativos...";
            if (_syncService is not null)
            {
                try
                {
                    await _syncService.DescargarRutaDelDiaAsync();
                }
                catch
                {
                    // Permitir ingreso offline con los datos locales en SQLCipher si el dispositivo está en Modo Avión
                }
            }

            if (_notificacionService is not null)
            {
                await _notificacionService.SolicitarPermisosNotificacionAsync();
            }

            if (_realtimeTrackingService is not null)
            {
                _ = _realtimeTrackingService.StartAsync();
            }

            var rol = resultado.Perfil ?? _authService.Perfil ?? Preferences.Default.Get("RolUsuario", string.Empty);

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
        }
        catch (Exception ex)
        {
            StatusMessage = null;
            ErrorMessage = $"Error al reanudar sesión con biometría: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
