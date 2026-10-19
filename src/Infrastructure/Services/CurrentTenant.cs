using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Infrastructure.Services;

public class CurrentTenant : ICurrentTenant
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly Guid OperadorDemoDefault = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UsuarioDemoDefault = Guid.Parse("77777777-7777-7777-7777-777777777777");

    public CurrentTenant(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid OperadorId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;
            var tenantClaim = user?.FindFirst("operador_id")?.Value
                ?? user?.FindFirst("tenant_id")?.Value
                ?? user?.FindFirst("OperadorId")?.Value;

            if (Guid.TryParse(tenantClaim, out var tenantId))
                return tenantId;

            // Revisar cabeceras HTTP de tenant (X-Tenant-Id o X-Operador-Id)
            var headerTenant = httpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault()
                ?? httpContext?.Request.Headers["X-Operador-Id"].FirstOrDefault();

            if (Guid.TryParse(headerTenant, out var headerTenantId))
                return headerTenantId;

            // Operador por defecto del sistema para desarrollo / pruebas locales / mobile demo
            return OperadorDemoDefault;
        }
    }

    public Guid? UsuarioId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;
            var userClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user?.FindFirst("sub")?.Value
                ?? user?.FindFirst("usuario_id")?.Value;

            if (Guid.TryParse(userClaim, out var userId))
                return userId;

            var headerUser = httpContext?.Request.Headers["X-User-Id"].FirstOrDefault()
                ?? httpContext?.Request.Headers["X-Usuario-Id"].FirstOrDefault();

            if (Guid.TryParse(headerUser, out var headerUserId))
                return headerUserId;

            return UsuarioDemoDefault;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? true;
}
