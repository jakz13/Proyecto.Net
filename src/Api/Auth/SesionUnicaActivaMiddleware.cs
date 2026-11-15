using System.Security.Claims;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Api.Auth;

/// <summary>
/// Valida la política de Sesión Única Activa por Usuario (Single Active Session).
/// Cuando un usuario inicia sesión en un nuevo dispositivo, la clave en Redis
/// <c>sesion:activa:usuario:{usuarioId}</c> se actualiza con el nuevo <c>sid</c>.
/// Cualquier solicitud autenticada proveniente de un token con un <c>sid</c> anterior
/// es rechazada inmediatamente con HTTP 401 Unauthorized y código <c>SESION_DESPLAZADA</c>.
/// Si Redis no está disponible, aplica degradación elegante permitiendo continuar la operación.
/// </summary>
public sealed class SesionUnicaActivaMiddleware
{
    public const string CodigoSesionDesplazada = "SESION_DESPLAZADA";
    public const string MensajeSesionDesplazada = "Sesión cerrada porque se inició sesión con esta cuenta en otro dispositivo.";

    private readonly RequestDelegate _siguiente;
    private readonly ILogger<SesionUnicaActivaMiddleware> _logger;

    public SesionUnicaActivaMiddleware(
        RequestDelegate siguiente,
        ILogger<SesionUnicaActivaMiddleware> logger)
    {
        _siguiente = siguiente;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        if (!EsRutaExenta(contexto.Request.Path)
            && contexto.User.Identity?.IsAuthenticated == true)
        {
            var usuarioId = ResolverUsuarioId(contexto);
            var sidToken = contexto.User.FindFirst("sid")?.Value
                        ?? contexto.User.FindFirst("jti")?.Value;

            if (usuarioId.HasValue && usuarioId.Value != Guid.Empty && !string.IsNullOrWhiteSpace(sidToken))
            {
                var cache = contexto.RequestServices.GetService<IDistributedCacheService>();
                if (cache is not null)
                {
                    string? sesionVigente = null;
                    try
                    {
                        sesionVigente = await cache.GetAsync<string>(
                            ClavesCache.SesionActivaUsuario(usuarioId.Value),
                            contexto.RequestAborted);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Degradación controlada al consultar sesión única activa en caché.");
                    }

                    if (!string.IsNullOrWhiteSpace(sesionVigente)
                        && !string.Equals(sesionVigente, sidToken, StringComparison.Ordinal))
                    {
                        _logger.LogInformation(
                            "Sesión desplazada detectada para usuario {UsuarioId} en ruta {Ruta}.",
                            usuarioId.Value,
                            contexto.Request.Path);

                        contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await contexto.Response.WriteAsJsonAsync(new
                        {
                            error = MensajeSesionDesplazada,
                            codigo = CodigoSesionDesplazada
                        }, contexto.RequestAborted);
                        return;
                    }
                }
            }
        }

        await _siguiente(contexto);
    }

    private static Guid? ResolverUsuarioId(HttpContext contexto)
    {
        var currentTenant = contexto.RequestServices.GetService<ICurrentTenant>();
        if (currentTenant?.UsuarioId is { } tenantUserId && tenantUserId != Guid.Empty)
        {
            return tenantUserId;
        }

        var rawSub = contexto.User.FindFirst("sub")?.Value
                  ?? contexto.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(rawSub, out var parsed) && parsed != Guid.Empty ? parsed : null;
    }

    private static bool EsRutaExenta(PathString ruta) =>
        ruta.StartsWithSegments("/live")
        || ruta.StartsWithSegments("/ready")
        || ruta.StartsWithSegments("/health")
        || ruta.StartsWithSegments("/metrics")
        || ruta.StartsWithSegments("/swagger")
        || ruta.StartsWithSegments("/api/v1/auth/login")
        || ruta.StartsWithSegments("/api/auth/login")
        || ruta.StartsWithSegments("/hubs/tablero");
}

