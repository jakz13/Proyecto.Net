using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Deposito.ClasificarBultos;
using UltimaMilla.Application.Commands.Deposito.RecepcionarBultos;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Api.Endpoints;

public static class DepositoEndpoints
{
    public static IEndpointRouteBuilder MapDepositoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deposito/envios/{envioId:guid}")
            .WithTags("Depósito (UC-14, UC-15)")
            .RequireAuthorization();

        group.MapPost("/recepcion", async (
            [FromRoute] Guid envioId,
            [FromBody] RecepcionarBultosRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new RecepcionarBultosCommand(
                    envioId,
                    request.CodigosBultosEscaneados ?? new List<string>(),
                    request.CodigosBultosDanados,
                    request.Observaciones);

                var resultado = await mediator.Send(command, ct);
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
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        })
        .WithName("RecepcionarBultos")
        .WithSummary("Recepcionar bultos de un envío en depósito")
        .WithDescription("Contrasta los bultos escaneados contra los declarados. Sin discrepancias el envío pasa a EnDeposito; con discrepancias se requiere documentarlas en las observaciones.")
        .Produces<RecepcionResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        group.MapPost("/clasificacion", async (
            [FromRoute] Guid envioId,
            [FromBody] ClasificarBultosRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new ClasificarBultosCommand(
                    envioId,
                    request.Bultos ?? new List<ClasificacionBultoDto>());

                var resultado = await mediator.Send(command, ct);
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
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        })
        .WithName("ClasificarBultos")
        .WithSummary("Clasificar bultos de un envío en depósito")
        .WithDescription("Registra zona, prioridad y tipo de cada bulto recepcionado. Cuando todos los bultos recibidos están clasificados el envío queda listo para una hoja de ruta.")
        .Produces<ClasificacionResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        return app;
    }
}

public record ClasificarBultosRequest(List<ClasificacionBultoDto>? Bultos);

public record RecepcionarBultosRequest(
    List<string>? CodigosBultosEscaneados,
    List<string>? CodigosBultosDanados = null,
    string? Observaciones = null);
