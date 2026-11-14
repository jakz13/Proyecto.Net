using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Auth;

/// <summary>
/// Un comercio dado de baja por el operador no puede seguir operando: el login ya lo rechaza, pero un token emitido
/// antes de la baja seguiría válido hasta expirar. Este filtro revisa en la base, en cada pedido de un usuario de
/// comercio, que su relación con el operador siga activa; si no, responde 403 y el portal cierra la sesión.
/// </summary>
public sealed class ComercioActivoMiddleware
{
    public const string CodigoError = "COMERCIO_DADO_DE_BAJA";

    private readonly RequestDelegate _siguiente;

    public ComercioActivoMiddleware(RequestDelegate siguiente) => _siguiente = siguiente;

    public async Task InvokeAsync(HttpContext contexto)
    {
        // Los servicios se piden solo para usuarios de comercio: el resto de los pedidos (salud, métricas, personal) no los toca
        if (!EsRutaTecnica(contexto.Request.Path)
            && contexto.User.Identity?.IsAuthenticated == true && contexto.User.IsInRole(PoliticasApi.RolComercio)
            && contexto.RequestServices.GetRequiredService<ICurrentTenant>().ComercioOperadorId is { } relacionId)
        {
            var db = contexto.RequestServices.GetRequiredService<AppDbContext>();
            var dadoDeBaja = await db.ComerciosOperadores.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(v => v.Id == relacionId && v.Estado != "Activo", contexto.RequestAborted);

            if (dadoDeBaja)
            {
                contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
                await contexto.Response.WriteAsJsonAsync(new
                {
                    codigo = CodigoError,
                    mensaje = "El comercio se encuentra dado de baja para este operador. No puede realizar más acciones."
                }, contexto.RequestAborted);
                return;
            }
        }

        await _siguiente(contexto);
    }

    private static bool EsRutaTecnica(PathString ruta) =>
        ruta.StartsWithSegments("/live") || ruta.StartsWithSegments("/ready") || ruta.StartsWithSegments("/health")
        || ruta.StartsWithSegments("/metrics") || ruta.StartsWithSegments("/swagger");
}
