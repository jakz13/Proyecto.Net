using System.Net.Http.Headers;

namespace UltimaMilla.PortalComercio.Client.Auth;

/// <summary>
/// DelegatingHandler que intercepta cada llamada saliente por HttpClient para:
/// 1. Adjuntar el token JWT en el encabezado Authorization: Bearer {token}.
/// 2. Inyectar el encabezado X-Correlation-Id para trazabilidad distribuida de cada acción.
/// </summary>
public class JwtAuthorizationHandler : DelegatingHandler
{
    private readonly ITokenStore _tokenStore;

    public JwtAuthorizationHandler(ITokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage peticion, CancellationToken cancelacion)
    {
        // 1. Inyección de token de autenticación
        var token = await _tokenStore.ObtenerTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // 2. Encabezado de correlación para observabilidad distribuida (Requerimiento 13)
        if (!peticion.Headers.Contains("X-Correlation-Id"))
        {
            peticion.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());
        }

        return await base.SendAsync(peticion, cancelacion);
    }
}
