using System.Net.Http.Headers;

namespace UltimaMilla.Mobile.Services.Handlers;

public class AuthDelegatingHandler : DelegatingHandler
{
    public const string AuthTokenKey = "auth_token";
    public const string CabeceraCorrelacion = "X-Correlation-Id";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            // RNF 6.3: Recuperación segura del token JWT persistido
            var token = await SecureStorage.Default.GetAsync(AuthTokenKey);

            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch (Exception)
        {
            // Si SecureStorage no está disponible o falla, la petición continúa sin token
        }

        // RNF 6.12 (UC-44): Identificador de correlación para seguir la operación desde la app móvil hasta el worker
        if (!request.Headers.Contains(CabeceraCorrelacion))
        {
            request.Headers.TryAddWithoutValidation(CabeceraCorrelacion, Guid.NewGuid().ToString("N"));
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
