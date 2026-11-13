using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Commands.Comercio.ConfirmarDevolucion;
using UltimaMilla.Application.Commands.Seguimiento.SolicitarReprogramacion;
using UltimaMilla.Application.Queries.Comercio.GenerarEtiquetasEnvios;
using UltimaMilla.Application.Queries.Comercio.ListarDevolucionesDelComercio;
using UltimaMilla.Application.Queries.Comercio.ListarEnviosDelComercio;
using UltimaMilla.Application.Queries.Envios.ObtenerDetalleEnvio;
using UltimaMilla.Application.Queries.Envios.ObtenerEvidenciaEntrega;
using UltimaMilla.Application.Queries.Seguimiento.ConsultarSeguimientoPublico;
using UltimaMilla.Application.Queries.Seguimiento.ListarFranjasDisponibles;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Api.Auth;

namespace UltimaMilla.Api.Endpoints;

public static class ConsultasEnvioEndpoints
{
    public const string PoliticaLimiteSeguimiento = "seguimiento-publico";

    public static IEndpointRouteBuilder MapConsultasEnvioEndpoints(this IEndpointRouteBuilder app)
    {
        // consulta pública y anónima; limitada por tasa y sin datos personales
        app.MapGet("/api/seguimiento/{codigoSeguimiento}", async (
            string codigoSeguimiento,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            var seguimiento = await mediator.Send(new ConsultarSeguimientoPublicoQuery(codigoSeguimiento), ct);

            // Respuesta genérica: no se distingue entre un código mal formado y uno inexistente
            return seguimiento is null
                ? Results.NotFound(new { error = "No se encontró un envío con el código indicado." })
                : Results.Ok(seguimiento);
        })
        .AllowAnonymous()
        .RequireRateLimiting(PoliticaLimiteSeguimiento)
        .WithTags("Seguimiento público")
        .WithName("ConsultarSeguimientoPublico")
        .WithSummary("Consulta pública del estado y la línea de tiempo de un envío")
        .Produces<SeguimientoPublicoDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status429TooManyRequests)
        .WithOpenApi();

        // el destinatario elige otra franja desde el enlace de seguimiento
        app.MapGet("/api/seguimiento/{codigoSeguimiento}/franjas", async (
            string codigoSeguimiento,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            var franjas = await mediator.Send(new ListarFranjasDisponiblesQuery(codigoSeguimiento), ct);
            return franjas is null
                ? Results.NotFound(new { error = "No se encontró un envío con el código indicado." })
                : Results.Ok(franjas);
        })
        .AllowAnonymous()
        .RequireRateLimiting(PoliticaLimiteSeguimiento)
        .WithTags("Seguimiento público")
        .WithName("ListarFranjasDisponibles")
        .WithSummary("Franjas horarias alternativas para reprogramar la entrega")
        .Produces<FranjasDisponiblesDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status429TooManyRequests)
        .WithOpenApi();

        app.MapPost("/api/seguimiento/{codigoSeguimiento}/reprogramacion", async (
            string codigoSeguimiento,
            [FromBody] SolicitarReprogramacionRequest request,
            HttpContext http,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var origen = http.Connection.RemoteIpAddress?.ToString();
                return Results.Ok(await mediator.Send(
                    new SolicitarReprogramacionCommand(codigoSeguimiento, request.FranjaHorariaId, origen), ct));
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ConflictoDeConcurrenciaException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting(PoliticaLimiteSeguimiento)
        .WithTags("Seguimiento público")
        .WithName("SolicitarReprogramacion")
        .WithSummary("Solicitar la reprogramación de la entrega")
        .WithDescription("Evalúa la solicitud contra las reglas del operador. Siempre responde 200 con el resultado: aceptada, o rechazada con su motivo.")
        .Produces<ResultadoReprogramacionDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status429TooManyRequests)
        .WithOpenApi();

        // Detalle interno para el Backoffice: historial completo y pruebas de entrega
        app.MapGet("/api/envios/{envioId:guid}/detalle", async (
            [FromRoute] Guid envioId,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await mediator.Send(new ObtenerDetalleEnvioQuery(envioId), ct));
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
        .RequireAuthorization(PoliticasApi.ConsultaDeEnvios)
        .WithTags("Envíos (Backoffice)")
        .WithName("ObtenerDetalleEnvio")
        .WithSummary("Detalle interno del envío con eventos, intentos y prueba de entrega")
        .Produces<DetalleEnvioDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // Firma y foto de un intento de entrega, servidas como imagen. Mismo aislamiento que el detalle (operador y comercio).
        app.MapGet("/api/envios/{envioId:guid}/intentos/{intentoId:guid}/evidencia/{tipo}", async (
            [FromRoute] Guid envioId,
            [FromRoute] Guid intentoId,
            [FromRoute] string tipo,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            if (!Enum.TryParse<TipoEvidenciaEntrega>(tipo, ignoreCase: true, out var tipoEvidencia) || !Enum.IsDefined(tipoEvidencia))
                return Results.BadRequest(new { error = "El tipo de evidencia debe ser 'firma' o 'foto'." });

            try
            {
                var evidencia = await mediator.Send(new ObtenerEvidenciaEntregaQuery(envioId, intentoId, tipoEvidencia), ct);
                return Results.File(evidencia.Contenido, evidencia.TipoContenido);
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
        .RequireAuthorization(PoliticasApi.ConsultaDeEnvios)
        .WithTags("Envíos (Backoffice)")
        .WithName("ObtenerEvidenciaEntrega")
        .WithSummary("Imagen de la firma o de la foto de un intento de entrega")
        .Produces(StatusCodes.Status200OK, contentType: "image/png")
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // el comercio genera las etiquetas de uno o más envíos en estado Admitido para adherir al bulto
        app.MapPost("/api/comercio/envios/etiquetas", async (
            [FromBody] GenerarEtiquetasEnviosRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var ids = request?.EnvioIds ?? Array.Empty<Guid>();
                var resultado = await mediator.Send(new GenerarEtiquetasEnviosQuery(ids), ct);
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
        .RequireAuthorization(PoliticasApi.ListadoDeEnvios)
        .WithTags("Portal del comercio")
        .WithName("GenerarEtiquetasEnvios")
        .WithSummary("Generar etiquetas y códigos de barras para uno o más envíos")
        .WithDescription("Devuelve las etiquetas individuales y códigos de barras por cada bulto de los envíos seleccionados en cualquier etapa de su ciclo de vida.")
        .Produces<EtiquetasGeneradasResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        // el comercio consulta y sigue únicamente sus propios envíos
        app.MapGet("/api/comercio/envios", async (
            [FromQuery] string? estado,
            [FromQuery] DateOnly? desde,
            [FromQuery] DateOnly? hasta,
            [FromQuery] string? texto,
            [FromQuery] int? pagina,
            [FromQuery] int? tamano,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var query = new ListarEnviosDelComercioQuery(estado, desde, hasta, texto, pagina ?? 1, tamano ?? 20);
                return Results.Ok(await mediator.Send(query, ct));
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
        .RequireAuthorization(PoliticasApi.Comercio)
        .WithTags("Portal del comercio")
        .WithName("ListarEnviosDelComercio")
        .WithSummary("Listado de los envíos propios del comercio")
        .WithDescription("Filtra por estado, rango de fechas de alta y texto (destinatario o código de seguimiento). Solo devuelve los envíos del comercio del usuario autenticado.")
        .Produces<PaginaEnviosComercioDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // el comercio consulta el estado de sus devoluciones y confirma la recepción del bulto
        app.MapGet("/api/comercio/devoluciones", async (
            [FromQuery] bool? cerradas,
            [FromQuery] int? pagina,
            [FromQuery] int? tamano,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var query = new ListarDevolucionesDelComercioQuery(cerradas ?? false, pagina ?? 1, tamano ?? 20);
                return Results.Ok(await mediator.Send(query, ct));
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
        .RequireAuthorization(PoliticasApi.Comercio)
        .WithTags("Portal del comercio")
        .WithName("ListarDevolucionesDelComercio")
        .WithSummary("Devoluciones del comercio con su estado")
        .Produces<PaginaDevolucionesDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        app.MapPost("/api/comercio/devoluciones/{envioId:guid}/confirmacion", async (
            [FromRoute] Guid envioId,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await mediator.Send(new ConfirmarDevolucionCommand(envioId), ct));
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
        .RequireAuthorization(PoliticasApi.Comercio)
        .WithTags("Portal del comercio")
        .WithName("ConfirmarDevolucionRecibida")
        .WithSummary("Confirmar la recepción del bulto devuelto")
        .WithDescription("Cierra la devolución desde la perspectiva del comercio: el envío pasa a Devuelto. Es idempotente y solo aplica a envíos propios en devolución.")
        .Produces<ConfirmarDevolucionResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status409Conflict)
        .WithOpenApi();

        return app;
    }
}

public record SolicitarReprogramacionRequest(Guid FranjaHorariaId);
public record GenerarEtiquetasEnviosRequest(IReadOnlyList<Guid>? EnvioIds);
