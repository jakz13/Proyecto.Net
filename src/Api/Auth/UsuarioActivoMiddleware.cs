using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Auth;

/// <summary>
/// Un token firmado sigue valiendo hasta que vence (8 horas), pero el administrador puede desactivar al usuario antes: desde ese momento
/// cualquier pedido con su token se rechaza con 401, y el siguiente inicio de sesión también (403). Antes, un usuario desactivado conservaba
/// el acceso hasta la expiración del token (hallazgo D-12 de la auditoría del 14/11).
/// La consulta es una lectura por clave primaria; los pedidos sin usuario en la base (modo demostración en localhost) no se tocan.
/// </summary>
public sealed class UsuarioActivoMiddleware
{
    public const string CodigoError = "USUARIO_DESACTIVADO";

    private readonly RequestDelegate _siguiente;

    public UsuarioActivoMiddleware(RequestDelegate siguiente) => _siguiente = siguiente;

    public async Task InvokeAsync(HttpContext contexto)
    {
        if (!EsRutaTecnica(contexto.Request.Path)
            && contexto.User.Identity?.IsAuthenticated == true
            && contexto.RequestServices.GetRequiredService<ICurrentTenant>().UsuarioId is { } usuarioId)
        {
            var db = contexto.RequestServices.GetRequiredService<AppDbContext>();
            var desactivado = await db.Usuarios.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(u => u.Id == usuarioId && !u.Activo, contexto.RequestAborted);

            if (desactivado)
            {
                contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await contexto.Response.WriteAsJsonAsync(new
                {
                    codigo = CodigoError,
                    mensaje = "El usuario fue desactivado. Inicie sesión nuevamente o consulte al administrador."
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
