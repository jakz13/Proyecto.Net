using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Operador.GestionUsuarios;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Queries.Operador.GestionUsuarios;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Usuarios;

namespace UltimaMilla.Api.Backoffice;

/// <summary>
/// Endpoints para el caso de uso UC-01: Gestión de usuarios y asignación de perfiles (RF-01, RF-31).
/// </summary>
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
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var (paginaActual, tamano) = RespuestasBackoffice.NormalizarPagina(pagina, tamanoPagina);
            var resultado = await mediator.Send(new ListarUsuariosQuery(q, perfilId, soloActivos, paginaActual, tamano), ct);

            return Results.Ok(new ResultadoPaginadoDto<UsuarioDto>
            {
                Elementos = resultado.Elementos.Select(Mapear).ToList(),
                TotalRegistros = resultado.TotalRegistros,
                PaginaActual = resultado.PaginaActual,
                TamanoPagina = resultado.TamanoPagina
            });
        }))
        .WithName("ListarUsuarios")
        .WithSummary("Listar usuarios del operador con filtros y paginación")
        .Produces<ResultadoPaginadoDto<UsuarioDto>>();

        grupo.MapGet("/{id:guid}", (
            Guid id,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var detalle = await mediator.Send(new ObtenerUsuarioPorIdQuery(id), ct);
            return Results.Ok(Mapear(detalle));
        }))
        .WithName("ObtenerUsuario")
        .WithSummary("Detalle de un usuario")
        .Produces<UsuarioDto>()
        .Produces(StatusCodes.Status404NotFound);

        grupo.MapPost("/", (
            [FromBody] CrearUsuarioDto dto,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var comando = new CrearUsuarioCommand(
                dto.Nombre,
                dto.Email,
                dto.Password,
                dto.PerfilId,
                dto.ComercioId
            );

            var creado = await mediator.Send(comando, ct);
            return Results.Created($"/api/usuarios/{creado.Id}", Mapear(creado));
        }))
        .WithName("CrearUsuario")
        .WithSummary("Registrar un usuario")
        .Produces<UsuarioDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        grupo.MapPut("/{id:guid}", (
            Guid id,
            [FromBody] ActualizarUsuarioDto dto,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var comando = new ActualizarUsuarioCommand(
                id,
                dto.Nombre,
                dto.Email,
                dto.PerfilId,
                dto.Activo
            );

            var actualizado = await mediator.Send(comando, ct);
            return Results.Ok(Mapear(actualizado));
        }))
        .WithName("ActualizarUsuario")
        .WithSummary("Modificar los datos de un usuario")
        .Produces<UsuarioDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        grupo.MapPatch("/{id:guid}/estado", (
            Guid id,
            [FromBody] CambioEstadoActivoRequest cuerpo,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            await mediator.Send(new CambiarEstadoUsuarioCommand(id, cuerpo.Activo), ct);
            return Results.NoContent();
        }))
        .WithName("CambiarEstadoUsuario")
        .WithSummary("Dar de alta o baja lógica a un usuario")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        grupo.MapPut("/{id:guid}/perfil", (
            Guid id,
            [FromBody] AsignarPerfilDto cuerpo,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            await mediator.Send(new AsignarPerfilUsuarioCommand(id, cuerpo.PerfilId), ct);
            return Results.NoContent();
        }))
        .WithName("AsignarPerfilUsuario")
        .WithSummary("Asignar o cambiar el perfil de un usuario")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/perfiles", (
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var perfiles = await mediator.Send(new ListarPerfilesQuery(), ct);
            return Results.Ok(perfiles.Select(p => new PerfilDto
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Descripcion = p.Descripcion
            }).ToList());
        }))
        .RequireAuthorization()
        .WithTags("Backoffice · Usuarios (UC-01)")
        .WithName("ListarPerfiles")
        .WithSummary("Perfiles disponibles para asignar a los usuarios")
        .Produces<IReadOnlyList<PerfilDto>>();

        return app;
    }

    private static UsuarioDto Mapear(UsuarioDetalleDto detalle) => new()
    {
        Id = detalle.Id,
        Nombre = detalle.Nombre,
        Email = detalle.Email,
        PerfilId = detalle.PerfilId,
        NombrePerfil = detalle.NombrePerfil,
        OperadorId = detalle.OperadorId,
        ComercioId = detalle.ComercioId,
        NombreComercio = detalle.NombreComercio,
        Activo = detalle.Activo,
        FechaAlta = detalle.FechaAlta
    };
}
