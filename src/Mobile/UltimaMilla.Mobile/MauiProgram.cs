using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using UltimaMilla.Mobile.Configuration;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Services.Api;
using UltimaMilla.Mobile.Services.Handlers;
using UltimaMilla.Mobile.Services.Sync;
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

namespace UltimaMilla.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // 1. Almacenamiento Local Cifrado (RNF 6.11 - SQLite con SQLCipher)
        builder.Services.AddSingleton<ILocalDatabaseService, LocalDatabaseService>();

        // 1b. Motor de Sincronización Resiliente Offline-First (UC-17 a UC-21)
        builder.Services.AddSingleton<ISyncService, SyncService>();

        // 2. Interceptor HTTP de Autenticación (RNF 6.3 - Inyección de Bearer token desde SecureStorage)
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
        builder.Services.AddTransient<IntentoFallidoViewModel>();
        builder.Services.AddTransient<CargaVehiculoViewModel>();

        // 5. Registro de Vistas / Páginas
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<DiagnosticPage>();
        builder.Services.AddTransient<RutaDiaPage>();
        builder.Services.AddTransient<DetalleParadaPage>();
        builder.Services.AddTransient<RegistroEntregaPage>();
        builder.Services.AddTransient<IntentoFallidoPage>();
        builder.Services.AddTransient<CargaVehiculoPage>();

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
