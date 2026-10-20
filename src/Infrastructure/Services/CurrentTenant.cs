using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Infrastructure.Services;

public class CurrentTenant : ICurrentTenant
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly Guid OperadorDemoDefault = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UsuarioDemoDefault = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid ComercioOperadorDemoDefault = Guid.Parse("33333333-3333-3333-3333-333333333333");

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

    public Guid? ComercioOperadorId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;
            var claim = user?.FindFirst("comercio_operador_id")?.Value
                ?? user?.FindFirst("comercio_id")?.Value;

            if (Guid.TryParse(claim, out var comercioId))
                return comercioId;

            var header = httpContext?.Request.Headers["X-Comercio-Id"].FirstOrDefault();
            if (Guid.TryParse(header, out var headerId))
                return headerId;

            // Comercio demo por defecto, igual que el operador y el usuario: solo para desarrollo y demo
            return ComercioOperadorDemoDefault;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? true;
}
