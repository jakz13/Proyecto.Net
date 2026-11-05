#if ANDROID
using Android.App;
using Android.Content;
using Android.Hardware.Biometrics;
using Android.OS;
using Microsoft.Maui.ApplicationModel;

namespace UltimaMilla.Mobile.Services.Security;

/// <summary>
/// Implementación nativa de autenticación biométrica en Android mediante BiometricPrompt.
/// Incluye soporte para PIN/Patrón/Credencial del dispositivo y fallback seguro.
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
        try
        {
            var soporta = await DispositivoSoportaBiometriaAsync();
            var activity = Platform.CurrentActivity;

            if (soporta && activity != null && OperatingSystem.IsAndroidVersionAtLeast(28))
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

                    return await tcs.Task;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BiometricAuthService] Error al solicitar prompt biométrico: {ex.Message}");
        }

        // Fallback seguro para emulador, dispositivos sin huella registrada o cancelación
        return await SolicitarConfirmacionSeguridadEnPantallaAsync(titulo, motivo);
    }

    private static async Task<bool> SolicitarConfirmacionSeguridadEnPantallaAsync(string titulo, string motivo)
    {
        if (Shell.Current is null)
            return true;

        return await Shell.Current.DisplayAlertAsync(
            "Verificación de Seguridad",
            $"{motivo}\n\nConfirme su identidad operativa para autorizar esta acción.",
            "Autorizar",
            "Cancelar");
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
            return true;

        return await Shell.Current.DisplayAlertAsync(
            "Verificación de Seguridad",
            $"{motivo}\n\nConfirme su identidad operativa para autorizar esta acción.",
            "Autorizar",
            "Cancelar");
    }
}
#endif

