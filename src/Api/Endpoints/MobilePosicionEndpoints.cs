using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Mobile.Posicion.RegistrarPosiciones;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Api.Endpoints;

public static class MobilePosicionEndpoints
{
    public static IEndpointRouteBuilder MapMobilePosicionEndpoints(this IEndpointRouteBuilder app)
    {
        // RF-23: la app envía la posición durante la jornada, en vivo o en lotes acumulados sin conexión
        app.MapPost("/api/mobile/v1/posiciones", async (
            [FromBody] RegistrarPosicionesCommand comando,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await mediator.Send(comando, ct));
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
        .RequireAuthorization()
        .WithTags("Mobile - Posición del vehículo (RF-23)")
        .WithName("RegistrarPosicionesVehiculo")
        .WithSummary("Registrar las posiciones del vehículo del repartidor autenticado")
        .WithDescription("Acepta una o varias posiciones (hasta 500) con el instante en que el dispositivo las tomó, para sincronizar lo acumulado sin conexión.")
        .Produces<RegistrarPosicionesResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
