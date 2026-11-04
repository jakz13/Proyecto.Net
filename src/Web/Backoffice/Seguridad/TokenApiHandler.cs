using System.Net.Http.Headers;

namespace UltimaMilla.Backoffice.Seguridad;

/// <summary>
/// Reenvía a la API el JWT que quedó guardado en la sesión del usuario al iniciar sesión.
/// Solo se registra cuando el modo demo está apagado (ver Program.cs).
/// </summary>
public sealed class TokenApiHandler : DelegatingHandler
{
    public const string ClaimToken = "api_access_token";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public TokenApiHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimToken)?.Value;
        if (!string.IsNullOrWhiteSpace(token) && request.Headers.Authorization is null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
