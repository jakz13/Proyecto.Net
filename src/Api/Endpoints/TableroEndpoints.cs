using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Tablero.EnviarInstruccionRepartidor;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Tablero;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Api.Endpoints;

public static class TableroEndpoints
{
    public static IEndpointRouteBuilder MapTableroEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/operacion/tablero")
            .WithTags("Tablero de operación en vivo (UC-12)")
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromServices] IMediator mediator,
            CancellationToken ct) => Results.Ok(await mediator.Send(new ObtenerTableroOperacionQuery(), ct)))
        .WithName("ObtenerTableroOperacion")
        .WithSummary("Tablero de operación en vivo del operador")
        .WithDescription("Hojas de ruta despachadas del operador con la última posición conocida de cada unidad y los envíos en riesgo de incumplir su ventana comprometida. La posición proviene del arribo a una parada o del último intento de entrega reportado; no hay telemetría GPS continua.")
        .Produces<TableroOperacionDto>(StatusCodes.Status200OK)
        .WithOpenApi();

        group.MapPost("/instrucciones", async (
            [FromBody] EnviarInstruccionRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new EnviarInstruccionRepartidorCommand(request.RepartidorId, request.Mensaje);
                var exito = await mediator.Send(command, ct);
                return Results.Ok(new { exito });
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: ex.Message);
            }
        })
        .WithName("EnviarInstruccionRepartidor")
        .WithSummary("Enviar instrucción prioritaria a la terminal móvil del repartidor")
        .WithDescription("Registra un evento de comunicación prioritaria para mitigar demoras o riesgo de ventana comprometida en la ruta en curso.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        return app;
    }
}
