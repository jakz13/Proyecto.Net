using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Planificacion.ArmarHojaDeRuta;
using UltimaMilla.Application.Exceptions;
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

        return app;
    }
}

public record ArmarHojaDeRutaRequest(
    Guid RepartidorId,
    Guid VehiculoId,
    DateOnly Fecha,
    List<Guid>? EnviosIds);
