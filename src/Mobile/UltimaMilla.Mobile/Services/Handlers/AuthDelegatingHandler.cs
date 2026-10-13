using System.Net.Http.Headers;

namespace UltimaMilla.Mobile.Services.Handlers;

public class AuthDelegatingHandler : DelegatingHandler
{
    public const string AuthTokenKey = "auth_token";

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

        return await base.SendAsync(request, cancellationToken);
    }
}
