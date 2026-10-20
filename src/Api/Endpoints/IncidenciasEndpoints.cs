using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Incidencias.AbrirIncidencia;
using UltimaMilla.Application.Commands.Incidencias.CerrarIncidencia;
using UltimaMilla.Application.Commands.Incidencias.RegistrarSeguimientoIncidencia;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Incidencias;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Api.Endpoints;

public static class IncidenciasEndpoints
{
    public static IEndpointRouteBuilder MapIncidenciasEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/backoffice/incidencias")
            .WithTags("Incidencias (UC-11)")
            .RequireAuthorization();

        group.MapPost("/", (
            [FromBody] AbrirIncidenciaRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) => Ejecutar(async () =>
        {
            var resultado = await mediator.Send(
                new AbrirIncidenciaCommand(request.Tipo ?? string.Empty, request.Descripcion ?? string.Empty, request.Severidad, request.EnvioId, request.HojaDeRutaId), ct);
            return Results.Created($"/api/backoffice/incidencias/{resultado.Id}", resultado);
        }))
        .WithName("AbrirIncidencia")
        .WithSummary("Registrar una incidencia (UC-11)")
        .Produces<IncidenciaResultDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        group.MapGet("/", (
            [FromQuery] string? estado,
            [FromQuery] string? tipo,
            [FromQuery] int? pagina,
            [FromQuery] int? tamanoPagina,
            [FromServices] IMediator mediator,
            CancellationToken ct) => Ejecutar(async () =>
                Results.Ok(await mediator.Send(new ListarIncidenciasQuery(estado, tipo, pagina ?? 1, tamanoPagina ?? 20), ct))))
        .WithName("ListarIncidencias")
        .WithSummary("Listar las incidencias del operador")
        .Produces<PaginaIncidenciasDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .WithOpenApi();

        group.MapGet("/{incidenciaId:guid}", (
            [FromRoute] Guid incidenciaId,
            [FromServices] IMediator mediator,
            CancellationToken ct) => Ejecutar(async () =>
                Results.Ok(await mediator.Send(new ObtenerIncidenciaQuery(incidenciaId), ct))))
        .WithName("ObtenerIncidencia")
        .WithSummary("Detalle de una incidencia con su bitácora")
        .Produces<IncidenciaDetalleDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        group.MapPost("/{incidenciaId:guid}/seguimiento", (
            [FromRoute] Guid incidenciaId,
            [FromBody] SeguimientoIncidenciaRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) => Ejecutar(async () =>
                Results.Ok(await mediator.Send(new RegistrarSeguimientoIncidenciaCommand(incidenciaId, request.Comentario, request.TomarACargo), ct))))
        .WithName("RegistrarSeguimientoIncidencia")
        .WithSummary("Agregar una nota a la bitácora o tomar la incidencia a cargo")
        .Produces<IncidenciaResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        group.MapPost("/{incidenciaId:guid}/cierre", (
            [FromRoute] Guid incidenciaId,
            [FromBody] CerrarIncidenciaRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) => Ejecutar(async () =>
                Results.Ok(await mediator.Send(new CerrarIncidenciaCommand(incidenciaId, request.Resolucion), ct))))
        .WithName("CerrarIncidencia")
        .WithSummary("Cerrar una incidencia indicando su resolución")
        .Produces<IncidenciaResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        return app;
    }

    private static async Task<IResult> Ejecutar(Func<Task<IResult>> accion)
    {
        try
        {
            return await accion();
        }
        catch (DomainValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (NotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (ConflictoDeConcurrenciaException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
    }
}

public record AbrirIncidenciaRequest(
    string? Tipo,
    string? Descripcion,
    string? Severidad = null,
    Guid? EnvioId = null,
    Guid? HojaDeRutaId = null);

public record SeguimientoIncidenciaRequest(string? Comentario = null, bool TomarACargo = false);

public record CerrarIncidenciaRequest(string? Resolucion);
