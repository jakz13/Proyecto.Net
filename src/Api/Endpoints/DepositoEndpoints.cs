using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Deposito.ClasificarBultos;
using UltimaMilla.Application.Commands.Deposito.RecepcionarBultos;
using UltimaMilla.Application.Commands.Deposito.RendirHojaDeRuta;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Deposito;
using UltimaMilla.Application.Queries.Deposito.ListarEnviosAdmitidos;
using UltimaMilla.Application.Queries.Deposito.ListarEnviosEnDeposito;
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

        // UC-16: rendición del repartidor al regresar a depósito
        app.MapPost("/api/deposito/hojas-de-ruta/{hojaDeRutaId:guid}/rendicion", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromBody] RendirHojaDeRutaRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new RendirHojaDeRutaCommand(
                    hojaDeRutaId,
                    request.CodigosBultosDevueltos ?? new List<string>(),
                    request.MotivoInternoCodigo,
                    request.Observaciones);

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
            catch (ConflictoDeConcurrenciaException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        })
        .RequireAuthorization()
        .WithTags("Depósito (UC-14, UC-15, UC-16)")
        .WithName("RendirHojaDeRuta")
        .WithSummary("Registrar la rendición del repartidor (UC-16)")
        .WithDescription("Contrasta los bultos entregados, devueltos y pendientes. Los envíos que volvieron sin intento quedan como no entregados con un motivo interno y vuelven a depósito; los reprogramados vuelven a depósito y los que están en devolución quedan pendientes de entrega al comercio. Cierra la ruta y habilita al repartidor.")
        .Produces<RendicionResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status409Conflict)
        .WithOpenApi();

        // UC-16: listado de hojas de ruta pendientes de rendición al regreso a depósito
        app.MapGet("/api/deposito/hojas-de-ruta-pendientes-rendicion", async (
            [FromServices] IMediator mediator,
            CancellationToken ct) => Results.Ok(await mediator.Send(new ListarRutasPendientesRendicionQuery(), ct)))
        .RequireAuthorization()
        .WithTags("Depósito (UC-14, UC-15, UC-16)")
        .WithName("ListarRutasPendientesRendicion")
        .WithSummary("Listar hojas de ruta pendientes de rendición")
        .WithDescription("Obtiene las hojas de ruta en estado Despachada, EnTransito o Finalizada del operador para procesar la rendición del repartidor al regreso.")
        .Produces<IReadOnlyList<RutaPendienteRendicionResumenDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // UC-16: detalle de hoja de ruta para rendición
        app.MapGet("/api/deposito/hojas-de-ruta/{hojaDeRutaId:guid}/rendicion", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await mediator.Send(new ObtenerRutaParaRendicionQuery(hojaDeRutaId), ct));
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
        .WithTags("Depósito (UC-14, UC-15, UC-16)")
        .WithName("ObtenerRutaParaRendicion")
        .WithSummary("Obtener detalle de hoja de ruta para rendición")
        .WithDescription("Obtiene los datos de la hoja de ruta, bultos que viajaron, entregas exitosas, bultos esperados de retorno y catálogo de motivos internos.")
        .Produces<RutaParaRendicionDetalleDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // UC-14: listado de envíos en estado Admitido pendientes de recepción física en depósito
        app.MapGet("/api/deposito/envios-admitidos", async (
            [FromServices] IMediator mediator,
            CancellationToken ct) => Results.Ok(await mediator.Send(new ListarEnviosAdmitidosQuery(), ct)))
        .RequireAuthorization()
        .WithTags("Depósito (UC-14, UC-15)")
        .WithName("ListarEnviosAdmitidos")
        .WithSummary("Listar envíos admitidos pendientes de recepción en depósito")
        .WithDescription("Obtiene los envíos en estado Admitido del operador actual para su posterior escaneo y recepción física.")
        .Produces<IReadOnlyList<EnvioAdmitidoDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // UC-15: listado de envíos en estado EnDeposito para clasificación de bultos
        app.MapGet("/api/deposito/envios-en-deposito", async (
            [FromServices] IMediator mediator,
            CancellationToken ct) => Results.Ok(await mediator.Send(new ListarEnviosEnDepositoQuery(), ct)))
        .RequireAuthorization()
        .WithTags("Depósito (UC-14, UC-15)")
        .WithName("ListarEnviosEnDeposito")
        .WithSummary("Listar envíos en depósito para clasificación de bultos")
        .WithDescription("Obtiene los envíos en estado EnDeposito del operador actual indicando el estado de clasificación de sus bultos.")
        .Produces<IReadOnlyList<EnvioEnDepositoDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        return app;
    }
}

public record RendirHojaDeRutaRequest(
    List<string>? CodigosBultosDevueltos,
    string? MotivoInternoCodigo = null,
    string? Observaciones = null);

public record ClasificarBultosRequest(List<ClasificacionBultoDto>? Bultos);

public record RecepcionarBultosRequest(
    List<string>? CodigosBultosEscaneados,
    List<string>? CodigosBultosDanados = null,
    string? Observaciones = null);
