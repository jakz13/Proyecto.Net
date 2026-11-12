using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Api.Auth;
using UltimaMilla.Application.Commands.Deposito.RecepcionarBultos;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Deposito.ListarEnviosAdmitidos;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Api.Endpoints;

public static class MobileDepositoEndpoints
{
    public static IEndpointRouteBuilder MapMobileDepositoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/mobile/v1/deposito")
            .WithTags("Mobile - Recepción en Depósito")
            .RequireAuthorization(PoliticasApi.PersonalDelOperador);

        // Listar envíos admitidos pendientes de recepción en depósito
        group.MapGet("/envios-admitidos", async (
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var envios = await mediator.Send(new ListarEnviosAdmitidosQuery(), ct);
                return Results.Ok(envios);
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        })
        .WithName("ListarEnviosAdmitidosMobile")
        .WithSummary("Listar envíos en estado admitido para recepción física en depósito")
        .Produces<IReadOnlyList<EnvioAdmitidoDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // Recepcionar bultos de un envío en depósito con contraste y discrepancias
        group.MapPost("/recepcionar", async (
            [FromBody] MobileRecepcionarBultosRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            if (request == null || request.EnvioId == Guid.Empty)
                return Results.BadRequest(new { error = "El identificador del envío es requerido." });

            try
            {
                // Si viene un tipo de discrepancia específico y no está reflejado en las observaciones, enriquecerlo
                var observaciones = request.Observaciones;
                if (!string.IsNullOrWhiteSpace(request.TipoDiscrepancia) &&
                    (string.IsNullOrWhiteSpace(observaciones) || !observaciones.Contains(request.TipoDiscrepancia, StringComparison.OrdinalIgnoreCase)))
                {
                    observaciones = string.IsNullOrWhiteSpace(observaciones)
                        ? $"Discrepancia reportada: {request.TipoDiscrepancia}"
                        : $"[{request.TipoDiscrepancia}] {observaciones}";
                }

                var command = new RecepcionarBultosCommand(
                    request.EnvioId,
                    request.CodigosBultosEscaneados ?? new List<string>(),
                    request.CodigosBultosDanados,
                    observaciones);

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
        .WithName("RecepcionarBultosMobile")
        .WithSummary("Recepcionar bultos de un envío en depósito desde la app móvil")
        .Produces<RecepcionResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        return app;
    }
}

public record MobileRecepcionarBultosRequest(
    Guid EnvioId,
    List<string>? CodigosBultosEscaneados,
    List<string>? CodigosBultosDanados = null,
    string? TipoDiscrepancia = null,
    string? Observaciones = null);

