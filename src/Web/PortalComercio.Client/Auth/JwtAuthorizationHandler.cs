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

    private readonly Microsoft.AspNetCore.Components.NavigationManager _navegador;

    public JwtAuthorizationHandler(ITokenStore tokenStore, Microsoft.AspNetCore.Components.NavigationManager navegador)
    {
        _tokenStore = tokenStore;
        _navegador = navegador;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage peticion, CancellationToken cancelacion)
    {
        // 1. Inyección de token de autenticación
        var token = await _tokenStore.ObtenerTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else if (peticion.Headers.Authorization is null)
        {
            // Token de desarrollo para peticiones anónimas en entorno local
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "dev-token");
        }

        // 2. Encabezado de correlación para observabilidad distribuida (Requerimiento 13)
        if (!peticion.Headers.Contains("X-Correlation-Id"))
        {
            peticion.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());
        }

        var respuesta = await base.SendAsync(peticion, cancelacion);

        // El operador dio de baja al comercio con la sesión abierta: la API responde 403 con este código.
        // Se cierra la sesión y se lleva al login con el aviso, en vez de dejar pantallas que fallan una por una.
        if (respuesta.StatusCode == System.Net.HttpStatusCode.Forbidden && respuesta.Content is not null)
        {
            var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);
            if (cuerpo.Contains("COMERCIO_DADO_DE_BAJA", StringComparison.Ordinal))
            {
                await _tokenStore.EliminarTokenAsync();
                _navegador.NavigateTo("/login?baja=1", forceLoad: true);
            }
        }

        return respuesta;
    }
}
