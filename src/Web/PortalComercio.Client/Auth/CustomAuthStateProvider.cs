using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace UltimaMilla.PortalComercio.Client.Auth;

/// <summary>
/// Proveedor de estado de autenticación personalizado para Blazor WebAssembly.
/// Extrae los claims del token JWT almacenado sin requerir bibliotecas externas adicionales.
/// </summary>
public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStore _tokenStore;
    private static readonly AuthenticationState EstadoAnonimo = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public CustomAuthStateProvider(ITokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenStore.ObtenerTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return EstadoAnonimo;
        }

        try
        {
            var reclamos = ParsearReclamosDesdeJwt(token);
            var identidad = new ClaimsIdentity(reclamos, "jwt");
            var usuario = new ClaimsPrincipal(identidad);

            return new AuthenticationState(usuario);
        }
        catch
        {
            // Si el token es corrupto o inválido, retornar estado no autenticado
            return EstadoAnonimo;
        }
    }

    public void NotificarAutenticacion()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task IniciarSesionSimuladaAsync(string nombre, string rol, string operadorId, string comercioId)
    {
        // Generar un payload JWT simplificado para testing en demo
        var payload = new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString(),
            ["name"] = nombre,
            ["role"] = rol,
            ["operador_id"] = operadorId,
            ["comercio_id"] = comercioId
        };

        var json = JsonSerializer.Serialize(payload);
        var base64UrlPayload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var tokenFicticio = $"eyJhbGciOiJIUzI1NiJ9.{base64UrlPayload}.demo-sig";
        await _tokenStore.GuardarTokenAsync(tokenFicticio);
        NotificarAutenticacion();
    }

    public async Task CerrarSesionAsync()
    {
        await _tokenStore.EliminarTokenAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(EstadoAnonimo));
    }

    private static IEnumerable<Claim> ParsearReclamosDesdeJwt(string jwt)
    {
        var partes = jwt.Split('.');
        if (partes.Length < 2)
        {
            return Enumerable.Empty<Claim>();
        }

        var payloadBase64 = partes[1]
            .Replace('-', '+')
            .Replace('_', '/');

        switch (payloadBase64.Length % 4)
        {
            case 2: payloadBase64 += "=="; break;
            case 3: payloadBase64 += "="; break;
        }

        var bytes = Convert.FromBase64String(payloadBase64);
        var paresClaveValor = JsonSerializer.Deserialize<Dictionary<string, object>>(bytes);

        if (paresClaveValor == null)
        {
            return Enumerable.Empty<Claim>();
        }

        var reclamos = new List<Claim>();
        foreach (var kvp in paresClaveValor)
        {
            var valorStr = kvp.Value?.ToString() ?? string.Empty;

            if (kvp.Key == "role")
            {
                reclamos.Add(new Claim(ClaimTypes.Role, valorStr));
            }
            else if (kvp.Key == "name")
            {
                reclamos.Add(new Claim(ClaimTypes.Name, valorStr));
            }
            else
            {
                reclamos.Add(new Claim(kvp.Key, valorStr));
            }
        }

        return reclamos;
    }
}
