using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace UltimaMilla.Api.Auth;

/// <summary>
/// Esquema de autenticación para desarrollo y demo móvil.
/// Permite que las llamadas de la app móvil hacia los endpoints de operación en calle
/// resuelvan automáticamente la identidad del repartidor y el OperadorId del seeder,
/// aceptando tokens Bearer enviados por AuthDelegatingHandler o peticiones directas en desarrollo.
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

        // Si se provee una cabecera Authorization Bearer con token JWT real, validarlo criptográficamente
        var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var tokenString = authHeader["Bearer ".Length..].Trim();
            var principalValido = UltimaMilla.Infrastructure.Services.TokenJwtService.ValidarYExtraerClaims(tokenString);
            if (principalValido is not null)
            {
                var ticketJwt = new AuthenticationTicket(principalValido, SchemeName);
                return Task.FromResult(AuthenticateResult.Success(ticketJwt));
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

        var operadorId = Guid.TryParse(operadorHeader, out var parsedOp)
            ? parsedOp.ToString()
            : "11111111-1111-1111-1111-111111111111";

        var usuarioHeader = httpContext.Request.Headers["X-User-Id"].FirstOrDefault()
            ?? httpContext.Request.Headers["X-Usuario-Id"].FirstOrDefault();

        var usuarioId = Guid.TryParse(usuarioHeader, out var parsedUser)
            ? parsedUser.ToString()
            : "77777777-7777-7777-7777-777777777777";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuarioId),
            new("sub", usuarioId),
            new("usuario_id", usuarioId),
            new("operador_id", operadorId),
            new("tenant_id", operadorId),
            new("OperadorId", operadorId),
            new(ClaimTypes.Role, "Repartidor"),
            new(ClaimTypes.Role, "Despachador"),
            new(ClaimTypes.Role, "Operario"),
            new(ClaimTypes.Role, "Administrador"),
            new(ClaimTypes.Name, "Repartidor Demo (Martín Rodríguez)")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
