using Microsoft.EntityFrameworkCore;
using UltimaMilla.Api.Auth;
using UltimaMilla.Application.Common;
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
            var comerciosIds = await ObtenerComerciosEquivalentesIdsAsync(db, tenant, ct);
            if (comerciosIds.Count == 0)
                return RespuestasBackoffice.Error(StatusCodes.Status403Forbidden, "La sesión no corresponde a un comercio.");

            var vinculados = await OperadoresActivosAsync(db, comerciosIds, ct);
            var vinculadosIds = vinculados.Select(o => o.Id).ToList();
            var identidades = vinculadosIds.Count == 0
                ? new List<UltimaMilla.Domain.Entities.Operador.IdentidadVisual>()
                : await db.IdentidadesVisuales.IgnoreQueryFilters().AsNoTracking()
                    .Where(i => vinculadosIds.Contains(i.OperadorId)).ToListAsync(ct);

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

            var operadorActualEfectivo = vinculadosIds.Contains(tenant.OperadorId)
                ? tenant.OperadorId
                : vinculadosIds.FirstOrDefault();

            return Results.Ok(new OperadoresDelComercioDto
            {
                OperadorActualId = operadorActualEfectivo == Guid.Empty ? tenant.OperadorId.ToString() : operadorActualEfectivo.ToString(),
                Operadores = lista
            });
        }))
        .WithName("ListarOperadoresDelComercio")
        .WithSummary("Operadores activos con los que trabaja el comercio de la sesión");

        grupo.MapPost("/{operadorId:guid}/sesion", (
            Guid operadorId,
            HttpContext httpContext,
            AppDbContext db,
            ICurrentTenant tenant,
            IUsuarioRepository usuarios,
            ITokenJwtService tokens,
            IDistributedCacheService cache,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var comerciosIds = await ObtenerComerciosEquivalentesIdsAsync(db, tenant, ct);
            if (comerciosIds.Count == 0)
                return RespuestasBackoffice.Error(StatusCodes.Status403Forbidden, "La sesión no corresponde a un comercio.");

            // Solo se puede operar con un operador con el que el comercio tiene un vínculo activo; de lo contrario no se revela que existe
            var vinculo = await db.ComerciosOperadores.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(v => comerciosIds.Contains(v.ComercioId) && v.OperadorId == operadorId && v.Estado == "Activo", ct);
            var operadorActivo = vinculo is not null
                && await db.Operadores.IgnoreQueryFilters().AnyAsync(o => o.Id == operadorId && o.Estado == "Activo", ct);
            if (!operadorActivo)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "El comercio no tiene un vínculo activo con ese operador.");

            var usuario = await usuarios.ObtenerPorIdAsync(tenant.UsuarioId!.Value, ct);
            if (usuario is null || !usuario.Activo)
                return RespuestasBackoffice.Error(StatusCodes.Status403Forbidden, "La cuenta no está disponible.");

            var perfil = await usuarios.ObtenerPerfilPorIdAsync(usuario.PerfilId, ct);
            var permisos = (await usuarios.ObtenerPermisosDePerfilAsync(usuario.PerfilId, ct)).Select(p => p.Codigo).ToList();

            // Conservar el identificador de sesión actual al cambiar de operador para no auto-desplazar la propia pestaña del usuario
            var sessionId = httpContext.User.FindFirst("sid")?.Value
                         ?? httpContext.User.FindFirst("jti")?.Value
                         ?? Guid.NewGuid().ToString("N");

            var token = tokens.GenerarToken(
                usuario,
                perfil ?? new UltimaMilla.Domain.Entities.Identidad.Perfil(usuario.PerfilId, PoliticasApi.RolComercio, string.Empty),
                permisos,
                comercioOperadorId: vinculo!.Id,
                operadorId: operadorId,
                sessionId: sessionId);

            // Actualizar vigencia de la sesión activa sin expulsar la conexión actual
            var aplicaSesionUnica = UltimaMilla.Application.Common.PoliticaSesionUnica.AplicaAlPerfil(perfil?.Nombre);
            if (aplicaSesionUnica)
            {
                await cache.SetAsync(
                    ClavesCache.SesionActivaUsuario(usuario.Id),
                    sessionId,
                    ClavesCache.ExpiracionSesionActiva,
                    ct);
            }

            return Results.Ok(new SesionOperadorDto { Token = token.Token, OperadorId = operadorId.ToString(), ExpiraUtc = token.ExpiraUtc });
        }))
        .WithName("CambiarOperadorDelComercio")
        .WithSummary("Abre sesión con otro de los operadores del comercio y devuelve el token nuevo");

        return app;
    }

    internal static async Task<List<Guid>> ObtenerOperadoresVinculadosIdsAsync(AppDbContext db, ICurrentTenant tenant, CancellationToken ct)
    {
        var comerciosIds = await ObtenerComerciosEquivalentesIdsAsync(db, tenant, ct);
        if (comerciosIds.Count == 0)
            return new List<Guid>();

        var vinculados = await OperadoresActivosAsync(db, comerciosIds, ct);
        return vinculados.Select(o => o.Id).ToList();
    }

    /// <summary>
    /// Obtiene el comercio del usuario autenticado y cualquier registro de comercio equivalente (mismo RUT normalizado en dígitos o mismo correo),
    /// para contemplar tanto vínculos sobre el mismo ComercioId como registros dados de alta con prefijo "RUT " distinto entre operadores.
    /// </summary>
    internal static async Task<List<Guid>> ObtenerComerciosEquivalentesIdsAsync(AppDbContext db, ICurrentTenant tenant, CancellationToken ct)
    {
        if (tenant.UsuarioId is not { } usuarioId)
            return new List<Guid>();

        var comercioId = await db.Usuarios.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => u.ComercioId)
            .FirstOrDefaultAsync(ct);

        if (comercioId is null || comercioId == Guid.Empty)
            return new List<Guid>();

        var principal = await db.Comercios.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == comercioId.Value, ct);
        if (principal is null)
            return new List<Guid> { comercioId.Value };

        var digitosRut = SoloDigitos(principal.Documento);
        var emailNorm = (principal.Email ?? string.Empty).Trim().ToLowerInvariant();

        var todosComercios = await db.Comercios.IgnoreQueryFilters().AsNoTracking()
            .Select(c => new { c.Id, c.Documento, c.Email })
            .ToListAsync(ct);

        var equivalentes = todosComercios
            .Where(c => c.Id == principal.Id
                || (digitosRut.Length >= 8 && SoloDigitos(c.Documento) == digitosRut)
                || (!string.IsNullOrWhiteSpace(emailNorm) && string.Equals((c.Email ?? string.Empty).Trim(), emailNorm, StringComparison.OrdinalIgnoreCase)))
            .Select(c => c.Id)
            .Distinct()
            .ToList();

        return equivalentes.Count > 0 ? equivalentes : new List<Guid> { comercioId.Value };
    }

    internal static string SoloDigitos(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? string.Empty : new string(texto.Where(char.IsAsciiDigit).ToArray());

    private static async Task<List<UltimaMilla.Domain.Entities.Operador.Operador>> OperadoresActivosAsync(
        AppDbContext db,
        IReadOnlyCollection<Guid> comerciosIds,
        CancellationToken ct)
    {
        var ids = await db.ComerciosOperadores.IgnoreQueryFilters().AsNoTracking()
            .Where(v => comerciosIds.Contains(v.ComercioId) && v.Estado == "Activo")
            .Select(v => v.OperadorId)
            .Distinct()
            .ToListAsync(ct);

        return await db.Operadores.IgnoreQueryFilters().AsNoTracking()
            .Where(o => ids.Contains(o.Id) && o.Estado == "Activo")
            .OrderBy(o => o.NombreComercial)
            .ToListAsync(ct);
    }
}
