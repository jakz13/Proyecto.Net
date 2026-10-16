using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Infrastructure.Services;

public class CurrentTenant : ICurrentTenant
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenant(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid OperadorId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var tenantClaim = user?.FindFirst("operador_id")?.Value
                ?? user?.FindFirst("tenant_id")?.Value
                ?? user?.FindFirst("OperadorId")?.Value;

            if (Guid.TryParse(tenantClaim, out var tenantId))
                return tenantId;

            // Operador por defecto del sistema para desarrollo / pruebas locales
            return Guid.Parse("11111111-1111-1111-1111-111111111111");
        }
    }

    public Guid? UsuarioId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user?.FindFirst("sub")?.Value
                ?? user?.FindFirst("usuario_id")?.Value;

            if (Guid.TryParse(userClaim, out var userId))
                return userId;

            return null;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
