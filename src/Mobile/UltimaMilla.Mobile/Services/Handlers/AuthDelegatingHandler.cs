using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Mobile.Services.Auth;

namespace UltimaMilla.Mobile.Services.Handlers;

public class AuthDelegatingHandler : DelegatingHandler
{
    public const string AuthTokenKey = "auth_token";
    public const string CabeceraCorrelacion = "X-Correlation-Id";
    public static readonly HttpRequestOptionsKey<bool> OpcionValidacionBiometrica = new("EsValidacionBiometrica");

    private readonly IServiceProvider? _serviceProvider;

    public AuthDelegatingHandler(IServiceProvider? serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Caché en memoria del token JWT activo para inyección inmediata en todas las peticiones HTTP,
    /// respaldado por SecureStorage del sistema operativo.
    /// </summary>
    public static string? CurrentTokenInMemory { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? tokenEnviado = request.Headers.Authorization?.Parameter;

        if (string.IsNullOrWhiteSpace(tokenEnviado))
        {
            try
            {
                var token = CurrentTokenInMemory;
                if (string.IsNullOrWhiteSpace(token))
                {
                    token = await SecureStorage.Default.GetAsync(AuthTokenKey);
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        CurrentTokenInMemory = token;
                    }
                }

                if (!string.IsNullOrWhiteSpace(token))
                {
                    tokenEnviado = token;
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }
            catch (Exception)
            {
                if (!string.IsNullOrWhiteSpace(CurrentTokenInMemory))
                {
                    tokenEnviado = CurrentTokenInMemory;
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CurrentTokenInMemory);
                }
            }
        }

        // Identificador de correlación para seguir la operación desde la app móvil hasta el worker
        if (!request.Headers.Contains(CabeceraCorrelacion))
        {
            request.Headers.TryAddWithoutValidation(CabeceraCorrelacion, Guid.NewGuid().ToString("N"));
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrWhiteSpace(tokenEnviado))
        {
            var ruta = request.RequestUri?.AbsolutePath ?? string.Empty;
            bool esRutaLogin = ruta.EndsWith("/auth/login", StringComparison.OrdinalIgnoreCase);
            bool esValidacionBiometrica = request.Options.TryGetValue(OpcionValidacionBiometrica, out var omitir) && omitir;

            if (!esRutaLogin &&
                !esValidacionBiometrica &&
                (string.IsNullOrWhiteSpace(CurrentTokenInMemory) ||
                 string.Equals(tokenEnviado, CurrentTokenInMemory, StringComparison.Ordinal)))
            {
                CurrentTokenInMemory = null;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var authService = _serviceProvider?.GetService<IAuthService>();
                        if (authService is not null)
                        {
                            await authService.ManejarSesionDesplazadaAsync();
                        }
                    }
                    catch
                    {
                    }
                });
            }
        }

        return response;
    }
}
