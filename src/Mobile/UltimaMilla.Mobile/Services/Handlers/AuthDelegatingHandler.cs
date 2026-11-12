using System.Net.Http.Headers;

namespace UltimaMilla.Mobile.Services.Handlers;

public class AuthDelegatingHandler : DelegatingHandler
{
    public const string AuthTokenKey = "auth_token";
    public const string CabeceraCorrelacion = "X-Correlation-Id";

    /// <summary>
    /// Caché en memoria del token JWT activo para inyección inmediata en todas las peticiones HTTP,
    /// respaldado por SecureStorage del sistema operativo.
    /// </summary>
    public static string? CurrentTokenInMemory { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
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
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch (Exception)
        {
            if (!string.IsNullOrWhiteSpace(CurrentTokenInMemory))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CurrentTokenInMemory);
            }
        }

        // Identificador de correlación para seguir la operación desde la app móvil hasta el worker
        if (!request.Headers.Contains(CabeceraCorrelacion))
        {
            request.Headers.TryAddWithoutValidation(CabeceraCorrelacion, Guid.NewGuid().ToString("N"));
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
