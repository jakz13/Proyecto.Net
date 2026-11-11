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
            System.Diagnostics.Debug.WriteLine($"[CRITICAL] AndroidEnvironment.UnhandledExceptionRaiser: {e.Exception}");
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[CRITICAL] AppDomain.UnhandledException: {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[CRITICAL] TaskScheduler.UnobservedTaskException: {e.Exception}");
            e.SetObserved();
        };
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
