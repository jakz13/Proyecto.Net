using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using UltimaMilla.Mobile.Configuration;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Services.Api;
using UltimaMilla.Mobile.ViewModels.Diagnostic;
using UltimaMilla.Mobile.Views.Diagnostic;

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

        // 1. Registro de Servicios de Almacenamiento Local (Offline-First)
        builder.Services.AddSingleton<ILocalDatabaseService, LocalDatabaseService>();

        // 2. Registro de Cliente HTTP Resiliente con Polly
        builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
        {
            client.BaseAddress = new Uri(ApiEndpoints.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .AddPolicyHandler(GetRetryPolicy());

        // 3. Registro de ViewModels
        builder.Services.AddTransient<DiagnosticViewModel>();

        // 4. Registro de Vistas / Páginas
        builder.Services.AddTransient<DiagnosticPage>();
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
