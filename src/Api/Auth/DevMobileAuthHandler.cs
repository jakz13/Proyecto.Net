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

        // Leer posible cabecera personalizada de tenant
        var operadorHeader = httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault()
            ?? httpContext.Request.Headers["X-Operador-Id"].FirstOrDefault();

        var operadorId = Guid.TryParse(operadorHeader, out var parsedOp)
            ? parsedOp.ToString()
            : "11111111-1111-1111-1111-111111111111";

        // Leer posible usuario personalizado
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
