using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Usuarios;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Services;

namespace UltimaMilla.Api.Backoffice;

/// <summary>Gestión de usuarios y asignación de perfiles (UC-01, RF-31).</summary>
public static class UsuariosEndpoints
{
    public static IEndpointRouteBuilder MapUsuariosBackofficeEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/usuarios")
            .WithTags("Backoffice · Usuarios (UC-01)")
            .RequireAuthorization();

        grupo.MapGet("/", (
            [FromQuery] int? pagina,
            [FromQuery] int? tamanoPagina,
            [FromQuery] string? q,
            [FromQuery] Guid? perfilId,
            [FromQuery] bool? soloActivos,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var (paginaActual, tamano) = RespuestasBackoffice.NormalizarPagina(pagina, tamanoPagina);
            var consulta = db.Usuarios.AsNoTracking().Where(u => u.OperadorId == tenant.OperadorId);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var patron = RespuestasBackoffice.PatronContiene(q);
                consulta = consulta.Where(u => EF.Functions.ILike(u.Nombre, patron, "\\") || EF.Functions.ILike(u.Email, patron, "\\"));
            }

            if (perfilId.HasValue && perfilId.Value != Guid.Empty)
                consulta = consulta.Where(u => u.PerfilId == perfilId.Value);

            if (soloActivos == true)
                consulta = consulta.Where(u => u.Activo);

            var pagina1 = await RespuestasBackoffice.PaginarAsync(consulta.OrderBy(u => u.Nombre).ThenBy(u => u.Id), paginaActual, tamano, ct);
            var elementos = await MapearAsync(db, pagina1.Elementos, ct);

            return Results.Ok(new ResultadoPaginadoDto<UsuarioDto>
            {
                Elementos = elementos,
                TotalRegistros = pagina1.TotalRegistros,
                PaginaActual = pagina1.PaginaActual,
                TamanoPagina = pagina1.TamanoPagina
            });
        }))
        .WithName("ListarUsuarios")
        .WithSummary("Listar usuarios del operador con filtros y paginación");

        grupo.MapGet("/{id:guid}", (Guid id, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
            RespuestasBackoffice.Ejecutar(async () =>
            {
                var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && u.OperadorId == tenant.OperadorId, ct);
                if (usuario is null)
                    return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el usuario solicitado.");

                return Results.Ok((await MapearAsync(db, new[] { usuario }, ct)).Single());
            }))
        .WithName("ObtenerUsuario")
        .WithSummary("Detalle de un usuario");

        grupo.MapPost("/", (
            [FromBody] CrearUsuarioDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();

            if (await db.Usuarios.AnyAsync(u => u.Email == email, ct))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "Ya existe un usuario registrado con ese correo electrónico.");

            if (!await db.Perfiles.AnyAsync(p => p.Id == dto.PerfilId, ct))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "El perfil seleccionado no existe.");

            if (dto.ComercioId.HasValue && !await db.ComerciosOperadores.AnyAsync(
                    co => co.ComercioId == dto.ComercioId.Value && co.OperadorId == tenant.OperadorId, ct))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "El comercio seleccionado no pertenece al operador.");

            var usuario = new Usuario(Guid.NewGuid(), dto.Nombre, email, HashContrasena.Generar(dto.Password),
                dto.PerfilId, tenant.OperadorId, dto.ComercioId);
            await db.Usuarios.AddAsync(usuario, ct);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/usuarios/{usuario.Id}", (await MapearAsync(db, new[] { usuario }, ct)).Single());
        }))
        .WithName("CrearUsuario")
        .WithSummary("Registrar un usuario");

        grupo.MapPut("/{id:guid}", (
            Guid id,
            [FromBody] ActualizarUsuarioDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id && u.OperadorId == tenant.OperadorId, ct);
            if (usuario is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el usuario solicitado.");

            var email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
            if (await db.Usuarios.AnyAsync(u => u.Email == email && u.Id != id, ct))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "Ya existe otro usuario con ese correo electrónico.");

            if (!await db.Perfiles.AnyAsync(p => p.Id == dto.PerfilId, ct))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "El perfil seleccionado no existe.");

            usuario.ActualizarDatos(dto.Nombre, email);
            usuario.AsignarPerfil(dto.PerfilId);
            if (dto.Activo) usuario.Activar(); else usuario.Desactivar();
            await db.SaveChangesAsync(ct);

            return Results.Ok((await MapearAsync(db, new[] { usuario }, ct)).Single());
        }))
        .WithName("ActualizarUsuario")
        .WithSummary("Modificar los datos de un usuario");

        grupo.MapPatch("/{id:guid}/estado", (
            Guid id,
            [FromBody] CambioEstadoActivoRequest cuerpo,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id && u.OperadorId == tenant.OperadorId, ct);
            if (usuario is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el usuario solicitado.");

            if (cuerpo.Activo) usuario.Activar(); else usuario.Desactivar();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }))
        .WithName("CambiarEstadoUsuario")
        .WithSummary("Dar de alta o baja lógica a un usuario");

        grupo.MapPut("/{id:guid}/perfil", (
            Guid id,
            [FromBody] AsignarPerfilDto cuerpo,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id && u.OperadorId == tenant.OperadorId, ct);
            if (usuario is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el usuario solicitado.");

            if (!await db.Perfiles.AnyAsync(p => p.Id == cuerpo.PerfilId, ct))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "El perfil seleccionado no existe.");

            usuario.AsignarPerfil(cuerpo.PerfilId);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }))
        .WithName("AsignarPerfilUsuario")
        .WithSummary("Asignar o cambiar el perfil de un usuario");

        app.MapGet("/api/perfiles", (AppDbContext db, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var perfiles = await db.Perfiles.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync(ct);
            return Results.Ok(perfiles.Select(p => new PerfilDto { Id = p.Id, Nombre = p.Nombre, Descripcion = p.Descripcion }).ToList());
        }))
        .RequireAuthorization()
        .WithTags("Backoffice · Usuarios (UC-01)")
        .WithName("ListarPerfiles")
        .WithSummary("Perfiles disponibles para asignar a los usuarios");

        return app;
    }

    /// <summary>Resuelve el nombre del perfil y del comercio de cada usuario con dos consultas en lote.</summary>
    private static async Task<List<UsuarioDto>> MapearAsync(AppDbContext db, IEnumerable<Usuario> usuarios, CancellationToken ct)
    {
        var lista = usuarios.ToList();
        var perfilIds = lista.Select(u => u.PerfilId).Distinct().ToList();
        var comercioIds = lista.Where(u => u.ComercioId.HasValue).Select(u => u.ComercioId!.Value).Distinct().ToList();

        var perfiles = await db.Perfiles.AsNoTracking().Where(p => perfilIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Nombre, ct);
        var comercios = await db.Comercios.AsNoTracking().Where(c => comercioIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => string.IsNullOrWhiteSpace(c.NombreFantasia) ? c.RazonSocial : c.NombreFantasia, ct);

        return lista.Select(u => new UsuarioDto
        {
            Id = u.Id,
            Nombre = u.Nombre,
            Email = u.Email,
            PerfilId = u.PerfilId,
            NombrePerfil = perfiles.GetValueOrDefault(u.PerfilId, string.Empty),
            OperadorId = u.OperadorId,
            ComercioId = u.ComercioId,
            NombreComercio = u.ComercioId.HasValue ? comercios.GetValueOrDefault(u.ComercioId.Value) : null,
            Activo = u.Activo,
            FechaAlta = u.FechaAlta
        }).ToList();
    }
}
