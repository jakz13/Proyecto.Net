using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Comercio.ConfigurarAvisos;
using UltimaMilla.Application.Commands.Comercio.ReintentarAviso;
using UltimaMilla.Application.Queries.Comercio.ConsultarHistorialAvisos;
using UltimaMilla.Application.Queries.Comercio.ObtenerConfiguracionAvisos;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Api.Auth;

namespace UltimaMilla.Api.Endpoints;

public static class AvisosComercioEndpoints
{
    public static IEndpointRouteBuilder MapAvisosComercioEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/comercio/avisos")
            .WithTags("Portal del comercio · Avisos automáticos y Webhooks")
            .RequireAuthorization(PoliticasApi.Comercio);

        // Consulta la configuración actual de suscripción a avisos
        grupo.MapGet("/", async (
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var resultado = await mediator.Send(new ObtenerConfiguracionAvisosQuery(), ct);
                return Results.Ok(resultado);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        })
        .WithName("ObtenerConfiguracionAvisos")
        .WithSummary("Consulta la configuración actual de webhooks del comercio")
        .Produces<DetalleConfiguracionAvisosDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden);

        // Consulta el historial de avisos enviados hacia los sistemas del comercio con filtros
        grupo.MapGet("/historial", async (
            [FromQuery] string? estado,
            [FromQuery] Guid? envioId,
            [FromQuery] string? codigoSeguimiento,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var consulta = new ConsultarHistorialAvisosQuery(estado, envioId, codigoSeguimiento);
                var resultado = await mediator.Send(consulta, ct);
                return Results.Ok(resultado);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        })
        .WithName("ConsultarHistorialAvisos")
        .WithSummary("Consulta el historial de entregas de avisos con soporte de filtros")
        .Produces<IReadOnlyList<AvisoHistorialDetalleDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden);

        // Solicita el reenvío manual de un aviso fallido o agotado
        grupo.MapPost("/{id:guid}/reintentar", async (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var comando = new ReintentarAvisoCommand(id);
                var resultado = await mediator.Send(comando, ct);
                return Results.Ok(resultado);
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        })
        .WithName("ReintentarAviso")
        .WithSummary("Solicita el reenvío manual de un aviso fallido o agotado")
        .Produces<ResultadoReintentoAvisoDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden);

        // Guarda o actualiza la suscripción a eventos
        grupo.MapPost("/", async (
            [FromBody] ConfigurarAvisosRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                if (request is null)
                {
                    return Results.BadRequest(new { error = "El cuerpo de la solicitud no puede ser vacío." });
                }

                var comando = new ConfigurarAvisosCommand(
                    request.UrlWebhook,
                    request.Eventos ?? Array.Empty<string>(),
                    request.ClaveFirma,
                    request.Activa);

                var resultado = await mediator.Send(comando, ct);
                return Results.Ok(resultado);
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
        .WithName("ConfigurarAvisos")
        .WithSummary("Configura la URL de webhook y los eventos suscritos")
        .Produces<ConfiguracionAvisosResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden);

        // Prueba simulada de webhook (Ping diagnóstico)
        grupo.MapPost("/probar", async (
            [FromBody] ProbarWebhookRequest? request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var comando = new ProbarWebhookCommand(request?.UrlWebhook);
                var resultado = await mediator.Send(comando, ct);
                return Results.Ok(resultado);
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
        .WithName("ProbarWebhookAvisos")
        .WithSummary("Envía una prueba de webhook con firma HMAC al endpoint del comercio")
        .Produces<ProbarWebhookResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}

public record ConfigurarAvisosRequest(
    string UrlWebhook,
    IReadOnlyList<string> Eventos,
    string? ClaveFirma,
    bool Activa = true);

public record ProbarWebhookRequest(
    string? UrlWebhook);
