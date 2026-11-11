using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using UltimaMilla.Mobile.Configuration;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Services.Api;
using UltimaMilla.Mobile.Services.Auth;
using UltimaMilla.Mobile.Services.Handlers;
using UltimaMilla.Mobile.Services.Notifications;
using UltimaMilla.Mobile.Services.Optimization;
using UltimaMilla.Mobile.Services.Realtime;
using UltimaMilla.Mobile.Services.Security;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.Services.Tracking;
using UltimaMilla.Mobile.ViewModels.Auth;
using UltimaMilla.Mobile.ViewModels.Carga;
using UltimaMilla.Mobile.ViewModels.Diagnostic;
using UltimaMilla.Mobile.ViewModels.Entrega;
using UltimaMilla.Mobile.ViewModels.Ruta;
using UltimaMilla.Mobile.Views.Auth;
using UltimaMilla.Mobile.Views.Carga;
using UltimaMilla.Mobile.Views.Diagnostic;
using UltimaMilla.Mobile.Views.Entrega;
using UltimaMilla.Mobile.Views.Ruta;
using ZXing.Net.Maui.Controls;

namespace UltimaMilla.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Inicializar proveedor nativo de SQLCipher (SQLitePCLRaw.bundle_e_sqlcipher)
        SQLitePCL.Batteries_V2.Init();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeReader()
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler(typeof(Shell), typeof(UltimaMilla.Mobile.Platforms.Android.CustomShellRenderer));
#endif
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // 1. Almacenamiento Local Cifrado (SQLite con SQLCipher)
        builder.Services.AddSingleton<ILocalDatabaseService, LocalDatabaseService>();
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IBiometricAuthService, BiometricAuthService>();
        builder.Services.AddSingleton<INotificacionLocalService, NotificacionLocalService>();

        // 1b. Motor de Sincronizacion Resiliente Offline-First
        builder.Services.AddSingleton<ISyncService, SyncService>();

        // 1c. Telemetria y Geolocalizacion en Tiempo Real con SignalR
        builder.Services.AddSingleton<IRealtimeTrackingService, RealtimeTrackingService>();
        builder.Services.AddSingleton<IGeofenceService, GeofenceService>();
        builder.Services.AddSingleton<IGeolocalizacionTrackingService, GeolocalizacionTrackingService>();
        builder.Services.AddSingleton<IRutaOptimizationService, RutaOptimizationService>();

        // 2. Interceptor HTTP de Autenticacion (Inyeccion de Bearer token desde SecureStorage)
        builder.Services.AddTransient<AuthDelegatingHandler>();

        // 3. Registro de Cliente HTTP genérico con el interceptor configurado
        builder.Services.AddHttpClient("BackendApi", client =>
        {
            client.BaseAddress = new Uri(ApiEndpoints.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<AuthDelegatingHandler>()
        .AddPolicyHandler(GetRetryPolicy());

        // Cliente tipado IApiClient existente
        builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
        {
            client.BaseAddress = new Uri(ApiEndpoints.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .AddHttpMessageHandler<AuthDelegatingHandler>()
        .AddPolicyHandler(GetRetryPolicy());

        // 4. Registro de ViewModels (CommunityToolkit.Mvvm)
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<DiagnosticViewModel>();
        builder.Services.AddTransient<RutaDiaViewModel>();
        builder.Services.AddTransient<DetalleParadaViewModel>();
        builder.Services.AddTransient<RegistroEntregaViewModel>();
        builder.Services.AddTransient<RegistroRetiroViewModel>();
        builder.Services.AddTransient<IntentoFallidoViewModel>();
        builder.Services.AddTransient<CargaVehiculoViewModel>();

        // 5. Registro de Vistas / Páginas
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<DiagnosticPage>();
        builder.Services.AddTransient<RutaDiaPage>();
        builder.Services.AddTransient<DetalleParadaPage>();
        builder.Services.AddTransient<RegistroEntregaPage>();
        builder.Services.AddTransient<RegistroRetiroPage>();
        builder.Services.AddTransient<IntentoFallidoPage>();
        builder.Services.AddTransient<CargaVehiculoPage>();
        builder.Services.AddTransient<EscanerCodigoPage>();

        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }

    /// <summary>
    /// Política de reintentos para soportar caídas momentáneas de red móvil (3G/4G/5G).
    /// Reintenta 3 veces con retroceso exponencial (2s, 4s, 8s).
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(3, retryAttempt => 
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
            );
    }
}
