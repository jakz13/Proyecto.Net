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
        else if (request.Headers.Authorization is null)
        {
            // En desarrollo local o demo sin token JWT explícito, enviar token dev y cabecera de tenant
            var tenantId = _httpContextAccessor.HttpContext?.User.FindFirst("tenant_id")?.Value
                ?? _httpContextAccessor.HttpContext?.User.FindFirst("operador_id")?.Value;
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
                request.Headers.TryAddWithoutValidation("X-Operador-Id", tenantId);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "dev-token");
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
