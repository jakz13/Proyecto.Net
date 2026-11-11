using Microsoft.EntityFrameworkCore;
using UltimaMilla.Api.Auth;
using UltimaMilla.Application.Ports;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Backoffice;

/// <summary>
/// Un mismo comercio (mismo RUT) puede trabajar con varios operadores. Estos endpoints le permiten ver con cuáles
/// y abrir sesión con cualquiera de ellos sin volver a iniciar sesión: el aislamiento por operador no se relaja,
/// la sesión nueva es la de un único operador, igual que la de un inicio de sesión normal.
/// </summary>
public static class ComercioOperadoresEndpoints
{
    public static IEndpointRouteBuilder MapComercioOperadoresEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/comercio/operadores")
            .RequireAuthorization(PoliticasApi.Comercio)
            .WithTags("Operadores del comercio");

        grupo.MapGet("/", (AppDbContext db, ICurrentTenant tenant, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var comercioId = await ComercioDelUsuarioAsync(db, tenant, ct);
            if (comercioId is null)
                return RespuestasBackoffice.Error(StatusCodes.Status403Forbidden, "La sesión no corresponde a un comercio.");

            var vinculados = await OperadoresActivosAsync(db, comercioId.Value, ct);
            var identidades = await db.IdentidadesVisuales.IgnoreQueryFilters().AsNoTracking()
                .Where(i => vinculados.Select(o => o.Id).Contains(i.OperadorId)).ToListAsync(ct);

            var porDefecto = new IdentidadVisualDto();
            var lista = vinculados.Select(o =>
            {
                var identidad = identidades.FirstOrDefault(i => i.OperadorId == o.Id);
                return new IdentidadVisualDto
                {
                    OperadorId = o.Id.ToString(),
                    NombreComercial = o.NombreComercial,
                    ColorPrimario = identidad?.ColorPrimario ?? porDefecto.ColorPrimario,
                    ColorSecundario = identidad?.ColorSecundario ?? porDefecto.ColorSecundario,
                    LogoUrl = identidad?.LogoUrl,
                    EmailContacto = identidad?.EmailContacto,
                    TelefonoContacto = identidad?.TelefonoContacto,
                    SitioWeb = identidad?.SitioWeb,
                    MensajeComunicaciones = identidad?.MensajeComunicaciones
                };
            }).ToList();

            return Results.Ok(new OperadoresDelComercioDto { OperadorActualId = tenant.OperadorId.ToString(), Operadores = lista });
        }))
        .WithName("ListarOperadoresDelComercio")
        .WithSummary("Operadores activos con los que trabaja el comercio de la sesión");

        grupo.MapPost("/{operadorId:guid}/sesion", (
            Guid operadorId,
            AppDbContext db,
            ICurrentTenant tenant,
            IUsuarioRepository usuarios,
            ITokenJwtService tokens,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var comercioId = await ComercioDelUsuarioAsync(db, tenant, ct);
            if (comercioId is null)
                return RespuestasBackoffice.Error(StatusCodes.Status403Forbidden, "La sesión no corresponde a un comercio.");

            // Solo se puede operar con un operador con el que el comercio tiene un vínculo activo; de lo contrario no se revela que existe
            var vinculo = await db.ComerciosOperadores.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(v => v.ComercioId == comercioId.Value && v.OperadorId == operadorId && v.Estado == "Activo", ct);
            var operadorActivo = vinculo is not null
                && await db.Operadores.IgnoreQueryFilters().AnyAsync(o => o.Id == operadorId && o.Estado == "Activo", ct);
            if (!operadorActivo)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "El comercio no tiene un vínculo activo con ese operador.");

            var usuario = await usuarios.ObtenerPorIdAsync(tenant.UsuarioId!.Value, ct);
            if (usuario is null || !usuario.Activo)
                return RespuestasBackoffice.Error(StatusCodes.Status403Forbidden, "La cuenta no está disponible.");

            var perfil = await usuarios.ObtenerPerfilPorIdAsync(usuario.PerfilId, ct);
            var permisos = (await usuarios.ObtenerPermisosDePerfilAsync(usuario.PerfilId, ct)).Select(p => p.Codigo).ToList();
            var token = tokens.GenerarToken(
                usuario,
                perfil ?? new UltimaMilla.Domain.Entities.Identidad.Perfil(usuario.PerfilId, PoliticasApi.RolComercio, string.Empty),
                permisos,
                comercioOperadorId: vinculo!.Id,
                operadorId: operadorId);

            return Results.Ok(new SesionOperadorDto { Token = token.Token, OperadorId = operadorId.ToString(), ExpiraUtc = token.ExpiraUtc });
        }))
        .WithName("CambiarOperadorDelComercio")
        .WithSummary("Abre sesión con otro de los operadores del comercio y devuelve el token nuevo");

        return app;
    }

    /// <summary>El comercio se toma del usuario autenticado (nunca de lo que envíe el cliente).</summary>
    private static async Task<Guid?> ComercioDelUsuarioAsync(AppDbContext db, ICurrentTenant tenant, CancellationToken ct)
    {
        if (tenant.UsuarioId is not { } usuarioId)
            return null;

        return await db.Usuarios.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == usuarioId).Select(u => u.ComercioId).FirstOrDefaultAsync(ct);
    }

    private static async Task<List<UltimaMilla.Domain.Entities.Operador.Operador>> OperadoresActivosAsync(AppDbContext db, Guid comercioId, CancellationToken ct)
    {
        var ids = await db.ComerciosOperadores.IgnoreQueryFilters().AsNoTracking()
            .Where(v => v.ComercioId == comercioId && v.Estado == "Activo").Select(v => v.OperadorId).ToListAsync(ct);

        return await db.Operadores.IgnoreQueryFilters().AsNoTracking()
            .Where(o => ids.Contains(o.Id) && o.Estado == "Activo").OrderBy(o => o.NombreComercial).ToListAsync(ct);
    }
}
