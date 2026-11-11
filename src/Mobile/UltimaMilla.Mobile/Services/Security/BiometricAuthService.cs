#if ANDROID
using System.Security.Cryptography;
using System.Text;
using Android.App;
using Android.Content;
using Android.Hardware.Biometrics;
using Android.OS;
using Microsoft.Maui.ApplicationModel;
using UltimaMilla.Mobile.Services.Auth;

namespace UltimaMilla.Mobile.Services.Security;

/// <summary>
/// Implementación nativa de autenticación biométrica en Android mediante BiometricPrompt.
/// Incluye soporte para PIN/Patrón/Credencial del dispositivo y validación con contraseña en producción.
/// </summary>
public class BiometricAuthService : IBiometricAuthService
{
    public Task<bool> DispositivoSoportaBiometriaAsync()
    {
        try
        {
            var context = Android.App.Application.Context;
            var keyguardManager = (KeyguardManager?)context.GetSystemService(Context.KeyguardService);
            if (keyguardManager == null)
                return Task.FromResult(false);

            return Task.FromResult(keyguardManager.IsDeviceSecure);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BiometricAuthService] Error al consultar soporte biométrico: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public async Task<bool> ValidarIdentidadAsync(string titulo, string motivo)
    {
        if (Shell.Current is null)
            return false;

        var activity = Platform.CurrentActivity;
        if (activity == null)
            return false;

        try
        {
            var soporta = await DispositivoSoportaBiometriaAsync();

            if (soporta && OperatingSystem.IsAndroidVersionAtLeast(28))
            {
                var tcs = new TaskCompletionSource<bool>();
                var mainExecutor = activity.MainExecutor;

                if (mainExecutor != null)
                {
                    var builder = new BiometricPrompt.Builder(activity)
                        .SetTitle(titulo)
                        .SetDescription(motivo);

                    if (OperatingSystem.IsAndroidVersionAtLeast(30))
                    {
                        builder.SetAllowedAuthenticators(
                            (int)(BiometricManagerAuthenticators.BiometricWeak |
                                  BiometricManagerAuthenticators.DeviceCredential));
                    }
                    else
                    {
                        builder.SetNegativeButton(
                            "Cancelar",
                            mainExecutor,
                            new DialogClickListener(tcs));
                    }

                    var prompt = builder.Build();
                    var cancellationSignal = new CancellationSignal();
                    var callback = new BiometricAuthenticationCallback(tcs);

                    prompt.Authenticate(cancellationSignal, mainExecutor, callback);

                    var resultadoPrompt = await tcs.Task;
                    if (resultadoPrompt)
                        return true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BiometricAuthService] Error al solicitar prompt biométrico: {ex.Message}");
        }

        // Si el hardware biométrico no está disponible, no hay huella registrada o falló el prompt:
#if DEBUG
        return await SolicitarConfirmacionSeguridadEnPantallaAsync(titulo, motivo);
#else
        return await SolicitarPasswordRespaldoAsync(titulo, motivo);
#endif
    }

#if DEBUG
    private static async Task<bool> SolicitarConfirmacionSeguridadEnPantallaAsync(string titulo, string motivo)
    {
        if (Shell.Current is null)
            return false;

        return await Shell.Current.DisplayAlertAsync(
            "Verificación de Seguridad",
            $"{motivo}\n\nConfirme su identidad operativa para autorizar esta acción.",
            "Autorizar",
            "Cancelar");
    }
#endif

    private static async Task<bool> SolicitarPasswordRespaldoAsync(string titulo, string motivo)
    {
        if (Shell.Current is null)
            return false;

        var storedHash = await SecureStorage.Default.GetAsync(AuthService.PasswordHashKey);
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        string? passwordIngresada = await Shell.Current.DisplayPromptAsync(
            titulo,
            $"{motivo}\n\nIngrese la contraseña de su cuenta para autorizar:",
            accept: "Validar",
            cancel: "Cancelar",
            placeholder: "Contraseña",
            maxLength: 64,
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(passwordIngresada))
            return false;

        using var sha = SHA256.Create();
        var inputHash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(passwordIngresada)));
        return string.Equals(storedHash, inputHash, StringComparison.Ordinal);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android28.0")]
    private sealed class DialogClickListener : Java.Lang.Object, IDialogInterfaceOnClickListener
    {
        private readonly TaskCompletionSource<bool> _tcs;

        public DialogClickListener(TaskCompletionSource<bool> tcs)
        {
            _tcs = tcs;
        }

        public void OnClick(IDialogInterface? dialog, int which)
        {
            _tcs.TrySetResult(false);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android28.0")]
    private sealed class BiometricAuthenticationCallback : BiometricPrompt.AuthenticationCallback
    {
        private readonly TaskCompletionSource<bool> _tcs;

        public BiometricAuthenticationCallback(TaskCompletionSource<bool> tcs)
        {
            _tcs = tcs;
        }

        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? result)
        {
            base.OnAuthenticationSucceeded(result);
            _tcs.TrySetResult(true);
        }

        public override void OnAuthenticationError(BiometricErrorCode errorCode, Java.Lang.ICharSequence? errString)
        {
            base.OnAuthenticationError(errorCode, errString);
            _tcs.TrySetResult(false);
        }

        public override void OnAuthenticationFailed()
        {
            base.OnAuthenticationFailed();
        }
    }
}
#else
using System.Security.Cryptography;
using System.Text;
using UltimaMilla.Mobile.Services.Auth;

namespace UltimaMilla.Mobile.Services.Security;

/// <summary>
/// Implementación para plataformas de escritorio (Windows) u otros entornos sin sensor biométrico móvil.
/// </summary>
public class BiometricAuthService : IBiometricAuthService
{
    public Task<bool> DispositivoSoportaBiometriaAsync()
    {
        return Task.FromResult(true);
    }

    public async Task<bool> ValidarIdentidadAsync(string titulo, string motivo)
    {
        if (Shell.Current is null)
            return false;

#if DEBUG
        return await Shell.Current.DisplayAlertAsync(
            "Verificación de Seguridad",
            $"{motivo}\n\nConfirme su identidad operativa para autorizar esta acción.",
            "Autorizar",
            "Cancelar");
#else
        return await SolicitarPasswordRespaldoAsync(titulo, motivo);
#endif
    }

    private static async Task<bool> SolicitarPasswordRespaldoAsync(string titulo, string motivo)
    {
        if (Shell.Current is null)
            return false;

        var storedHash = await SecureStorage.Default.GetAsync(AuthService.PasswordHashKey);
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        string? passwordIngresada = await Shell.Current.DisplayPromptAsync(
            titulo,
            $"{motivo}\n\nIngrese la contraseña de su cuenta para autorizar:",
            accept: "Validar",
            cancel: "Cancelar",
            placeholder: "Contraseña",
            maxLength: 64,
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(passwordIngresada))
            return false;

        using var sha = SHA256.Create();
        var inputHash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(passwordIngresada)));
        return string.Equals(storedHash, inputHash, StringComparison.Ordinal);
    }
}
#endif
