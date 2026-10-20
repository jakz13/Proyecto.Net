using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Planificacion.ArmarHojaDeRuta;
using UltimaMilla.Application.Commands.Planificacion.DespacharHojaDeRuta;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Planificacion;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Api.Endpoints;

public static class PlanificacionEndpoints
{
    public static IEndpointRouteBuilder MapPlanificacionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/planificacion/hojas-de-ruta")
            .WithTags("Planificación (UC-08, UC-09, UC-10)")
            .RequireAuthorization();

        group.MapPost("/", async (
            [FromBody] ArmarHojaDeRutaRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new ArmarHojaDeRutaCommand(
                    request.RepartidorId,
                    request.VehiculoId,
                    request.Fecha,
                    request.EnviosIds ?? new List<Guid>());

                var resultado = await mediator.Send(command, ct);
                return Results.Created($"/api/planificacion/hojas-de-ruta/{resultado.HojaDeRutaId}", resultado);
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
        })
        .WithName("ArmarHojaDeRuta")
        .WithSummary("Armar y liberar una hoja de ruta")
        .WithDescription("Asigna los envíos en depósito a un repartidor y un vehículo validando paradas máximas, capacidad y franjas. Los envíos que no cumplen se excluyen y se informa el motivo. La ruta queda liberada para su descarga.")
        .Produces<ArmarHojaDeRutaResultDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status409Conflict)
        .WithOpenApi();

        group.MapPost("/validacion", async (
            [FromBody] ArmarHojaDeRutaRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new ArmarHojaDeRutaCommand(
                    request.RepartidorId, request.VehiculoId, request.Fecha, request.EnviosIds ?? new List<Guid>(), SoloValidar: true);
                return Results.Ok(await mediator.Send(command, ct));
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        })
        .WithName("ValidarArmadoHojaDeRuta")
        .WithSummary("Validar el armado de una hoja de ruta sin guardarla")
        .WithDescription("Aplica las restricciones de paradas, capacidad y franjas y devuelve qué envíos entrarían y cuáles se excluirían, sin asignar nada.")
        .Produces<ArmarHojaDeRutaResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        group.MapGet("/", async (
            [FromQuery] DateOnly? fecha,
            [FromQuery] Guid? repartidorId,
            [FromQuery] string? estado,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            // El filtro de estado admite varios valores separados por coma: estado=Liberada,EnCarga
            var estados = string.IsNullOrWhiteSpace(estado)
                ? null
                : estado.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return Results.Ok(await mediator.Send(new ListarHojasDeRutaQuery(fecha, repartidorId, estados), ct));
        })
        .WithName("ListarHojasDeRuta")
        .WithSummary("Listar las hojas de ruta del operador")
        .Produces<IReadOnlyList<HojaDeRutaResumenDto>>(StatusCodes.Status200OK)
        .WithOpenApi();

        group.MapGet("/{hojaDeRutaId:guid}", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await mediator.Send(new ObtenerHojaDeRutaQuery(hojaDeRutaId), ct));
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        })
        .WithName("ObtenerHojaDeRuta")
        .WithSummary("Detalle de una hoja de ruta con sus paradas ordenadas")
        .Produces<HojaDeRutaDetalleDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        group.MapGet("/{hojaDeRutaId:guid}/verificacion-despacho", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await mediator.Send(new ObtenerVerificacionDespachoQuery(hojaDeRutaId), ct));
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        })
        .WithName("ObtenerVerificacionDespacho")
        .WithSummary("Control de carga previo al despacho (UC-09)")
        .WithDescription("Indica qué bultos de la ruta fueron cargados según el escaneo del repartidor, si hay otra ruta sin rendir y si la ruta se puede despachar.")
        .Produces<VerificacionDespachoDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        app.MapGet("/api/planificacion/envios-pendientes", async (
            [FromServices] IMediator mediator,
            CancellationToken ct) => Results.Ok(await mediator.Send(new ListarEnviosPendientesDeRutaQuery(), ct)))
        .RequireAuthorization()
        .WithTags("Planificación (UC-08, UC-09, UC-10)")
        .WithName("ListarEnviosPendientesDeRuta")
        .WithSummary("Envíos en depósito, clasificados, listos para asignar a una hoja de ruta")
        .Produces<IReadOnlyList<EnvioPendienteDeRutaDto>>(StatusCodes.Status200OK)
        .WithOpenApi();

        group.MapPost("/{hojaDeRutaId:guid}/despacho", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromBody] DespacharHojaDeRutaRequest? request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var resultado = await mediator.Send(new DespacharHojaDeRutaCommand(hojaDeRutaId, request?.Observaciones), ct);
                return Results.Ok(resultado);
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
        })
        .WithName("DespacharHojaDeRuta")
        .WithSummary("Despachar una hoja de ruta (UC-09)")
        .WithDescription("El operario confirma la salida del vehículo: los envíos con todos sus bultos cargados pasan a EnTransito y los demás vuelven a depósito. Es idempotente.")
        .Produces<DespachoResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status409Conflict)
        .WithOpenApi();

        return app;
    }
}

public record DespacharHojaDeRutaRequest(string? Observaciones = null);

public record ArmarHojaDeRutaRequest(
    Guid RepartidorId,
    Guid VehiculoId,
    DateOnly Fecha,
    List<Guid>? EnviosIds);
