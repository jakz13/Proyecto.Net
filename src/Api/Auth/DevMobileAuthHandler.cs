using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace UltimaMilla.Api.Auth;

/// <summary>
/// Esquema de autenticación para desarrollo y demo móvil/comercio.
/// Permite que las llamadas hacia los endpoints autenticados resuelvan la identidad del usuario,
/// el OperadorId y el ComercioId del seeder o del token de sesión en desarrollo,
/// aceptando tokens Bearer, access_token en SignalR (/hubs) o peticiones directas en desarrollo.
/// </summary>
public class DevMobileAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevMobileAuth";

    public DevMobileAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var httpContext = Context;

        // Si se provee una cabecera Authorization Bearer o access_token en SignalR (/hubs), validarlo criptográficamente
        var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();
        var tokenString = !string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..].Trim()
            : (httpContext.Request.Path.StartsWithSegments("/hubs")
                ? httpContext.Request.Query["access_token"].FirstOrDefault()?.Trim()
                : null);

        if (!string.IsNullOrWhiteSpace(tokenString))
        {
            var principalValido = UltimaMilla.Infrastructure.Services.TokenJwtService.ValidarYExtraerClaims(tokenString);
            if (principalValido is not null)
            {
                var ticketJwt = new AuthenticationTicket(principalValido, SchemeName);
                return Task.FromResult(AuthenticateResult.Success(ticketJwt));
            }

            // Si el token es un token de sesión de desarrollo del Portal del Comercio o Backoffice (termina en .demo-firma-jwt o .demo-sig),
            // extraer los claims de su payload JSON en Base64Url para respetar el comercio_id y operador_id del usuario autenticado
            if (tokenString.EndsWith(".demo-firma-jwt", StringComparison.OrdinalIgnoreCase) ||
                tokenString.EndsWith(".demo-sig", StringComparison.OrdinalIgnoreCase))
            {
                var principalDemo = ExtraerClaimsDeTokenDemo(tokenString);
                if (principalDemo is not null)
                {
                    var ticketDemo = new AuthenticationTicket(principalDemo, SchemeName);
                    return Task.FromResult(AuthenticateResult.Success(ticketDemo));
                }
            }

            // Si el token enviado no es un token simulado de desarrollo y falló la validación
            if (!tokenString.Equals("dev-token", StringComparison.OrdinalIgnoreCase) &&
                !tokenString.Equals("dev-demo-token", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.Fail("Token JWT inválido o expirado."));
            }
        }

        // Modo de desarrollo / demo: lectura de cabeceras personalizadas de tenant y usuario
        var operadorHeader = httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault()
            ?? httpContext.Request.Headers["X-Operador-Id"].FirstOrDefault();

        // En el Hub de SignalR (/hubs), si no se envió JWT ni cabecera explícita de operador, no autenticar anónimamente
        if (httpContext.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(operadorHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var operadorId = Guid.TryParse(operadorHeader, out var parsedOp)
            ? parsedOp.ToString()
            : "11111111-1111-1111-1111-111111111111";

        var usuarioHeader = httpContext.Request.Headers["X-User-Id"].FirstOrDefault()
            ?? httpContext.Request.Headers["X-Usuario-Id"].FirstOrDefault();

        var usuarioId = Guid.TryParse(usuarioHeader, out var parsedUser)
            ? parsedUser.ToString()
            : "77777777-7777-7777-7777-777777777777";

        var comercioId = "33333333-3333-3333-3333-333333333333";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuarioId),
            new("sub", usuarioId),
            new("usuario_id", usuarioId),
            new("operador_id", operadorId),
            new("tenant_id", operadorId),
            new("OperadorId", operadorId),
            new("comercio_id", comercioId),
            new("ComercioId", comercioId),
            new(ClaimTypes.Role, "Repartidor"),
            new(ClaimTypes.Role, "Despachador"),
            new(ClaimTypes.Role, "Operario"),
            new(ClaimTypes.Role, "Comercio"),
            new(ClaimTypes.Role, "Administrador"),
            new(ClaimTypes.Name, "Repartidor Demo (Martín Rodríguez)")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static ClaimsPrincipal? ExtraerClaimsDeTokenDemo(string token)
    {
        try
        {
            var partes = token.Split('.');
            if (partes.Length < 2)
            {
                return null;
            }

            var payloadBase64 = partes[1].Replace('-', '+').Replace('_', '/');
            switch (payloadBase64.Length % 4)
            {
                case 2: payloadBase64 += "=="; break;
                case 3: payloadBase64 += "="; break;
            }

            var jsonBytes = Convert.FromBase64String(payloadBase64);
            using var documento = System.Text.Json.JsonDocument.Parse(jsonBytes);
            var raiz = documento.RootElement;

            var claims = new List<Claim>();
            foreach (var propiedad in raiz.EnumerateObject())
            {
                var valor = propiedad.Value.ValueKind == System.Text.Json.JsonValueKind.String
                    ? propiedad.Value.GetString()
                    : propiedad.Value.ToString();

                if (string.IsNullOrWhiteSpace(valor))
                {
                    continue;
                }

                claims.Add(new Claim(propiedad.Name, valor));

                if (propiedad.Name.Equals("sub", StringComparison.OrdinalIgnoreCase))
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, valor));
                }
                else if (propiedad.Name.Equals("name", StringComparison.OrdinalIgnoreCase))
                {
                    claims.Add(new Claim(ClaimTypes.Name, valor));
                }
                else if (propiedad.Name.Equals("role", StringComparison.OrdinalIgnoreCase))
                {
                    claims.Add(new Claim(ClaimTypes.Role, valor));
                }
            }

            var identidad = new ClaimsIdentity(claims, SchemeName);
            return new ClaimsPrincipal(identidad);
        }
        catch
        {
            return null;
        }
    }
}
