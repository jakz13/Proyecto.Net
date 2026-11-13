using Android.App;
using Android.Runtime;

namespace UltimaMilla.Mobile;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
        AndroidEnvironment.UnhandledExceptionRaiser += (s, e) =>
        {
            RegistrarFalloCritico("AndroidEnvironment", e.Exception);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            RegistrarFalloCritico("AppDomain", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            RegistrarFalloCritico("TaskScheduler", e.Exception);
            e.SetObserved();
        };
    }

    private static void RegistrarFalloCritico(string origen, Exception? ex)
    {
        if (ex is null)
            return;

        var resumen = $"[{DateTime.UtcNow:O}] {origen}: {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}";
        System.Diagnostics.Debug.WriteLine($"[CRITICAL] {resumen}");
        try
        {
            var ruta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ultimo_crash.log");
            File.WriteAllText(ruta, resumen);
        }
        catch
        {
            // Evitar re-lanzar errores desde el registrador de fallos
        }
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
