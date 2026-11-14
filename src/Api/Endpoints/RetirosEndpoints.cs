using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Api.Auth;
using UltimaMilla.Application.Commands.AltaEnvio;
using UltimaMilla.Application.Commands.Retiros.GestionRetiros;
using UltimaMilla.Application.Commands.Retiros.RegistrarRetiroMovil;
using UltimaMilla.Application.Commands.Retiros.SolicitarRetiro;
using UltimaMilla.Application.DTOs.Retiros;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Retiros;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Api.Endpoints;

/// <summary>
/// Retiro en domicilio (cambio de requerimientos 2.2). A pedido del comercio, el operador retira bultos en el domicilio
/// del comprador y los hace llegar al comercio. El comercio lo solicita desde el portal o por API; el operador lo planifica en hojas de
/// ruta mixtas; el repartidor lo registra en la calle, también sin conectividad.
/// </summary>
public static class RetirosEndpoints
{
    /// <summary>Traduce las excepciones de negocio a respuestas HTTP, igual que el resto de los endpoints.</summary>
    private static async Task<IResult> EjecutarAsync(Func<Task<IResult>> accion, Func<Exception, IResult?>? adicional = null)
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

    private static SolicitarRetiroCommand ACommand(SolicitarRetiroRequest r) => new(
        Guid.Empty,
        new ContactoRetiroDto(r.NombreContacto ?? string.Empty, r.TelefonoContacto, r.EmailContacto),
        new DireccionAltaDto(r.Calle ?? string.Empty, r.Numero ?? string.Empty, r.Ciudad ?? string.Empty, r.ZonaCoberturaId, r.Latitud, r.Longitud, r.Referencia),
        // Un elemento nulo en la lista de bultos es un dato mal formado: 400, no 500
        r.Bultos?.Select(b => b is null
            ? throw new DomainValidationException("Cada bulto debe indicar su descripción, su peso y sus dimensiones.")
            : new BultoRetiroAltaDto(b.Descripcion, b.Peso, b.Alto, b.Ancho, b.Profundo)).ToList() ?? new List<BultoRetiroAltaDto>(),
        r.Motivo ?? string.Empty,
        r.FranjaHorariaId,
        r.FechaSolicitada,
        r.ReferenciaExterna);

    public static IEndpointRouteBuilder MapRetirosEndpoints(this IEndpointRouteBuilder app)
    {
        // =====================================================================
        // PORTAL DEL COMERCIO: el comercio solicita y sigue sus retiros
        // =====================================================================
        var portal = app.MapGroup("/api/comercio/retiros")
            .WithTags("Retiro en domicilio · Portal del comercio")
            .RequireAuthorization(PoliticasApi.Comercio);

        portal.MapPost("/", (
            [FromBody] SolicitarRetiroRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) => EjecutarAsync(async () =>
        {
            var resultado = await mediator.Send(ACommand(request), ct);
            return resultado.Duplicado
                ? Results.Ok(resultado)
                : Results.Created($"/api/comercio/retiros/{resultado.Id}", resultado);
        }))
        .WithName("SolicitarRetiro")
        .WithSummary("El comercio solicita el retiro en domicilio de uno o más bultos")
        .WithDescription("Indica bultos, domicilio, franja horaria y motivo. La tarifa se calcula con la regla de modalidad 'Retiro' del cuadro vigente. Si se informa referenciaExterna, repetir la solicitud devuelve la ya registrada (200).")
        .Produces<SolicitarRetiroResultDto>(StatusCodes.Status201Created)
        .Produces<SolicitarRetiroResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .WithOpenApi();

        portal.MapGet("/", (
            [FromQuery] EstadoRetiro? estado,
            [FromQuery] int? pagina,
            [FromQuery] int? tamano,
            [FromServices] IMediator mediator,
            CancellationToken ct) => EjecutarAsync(async () =>
                Results.Ok(await mediator.Send(new ListarRetirosDelComercioQuery(estado, pagina ?? 1, tamano ?? 20), ct))))
        .WithName("ListarRetirosDelComercio")
        .WithSummary("Retiros del comercio autenticado")
        .Produces<PaginaRetirosDto>(StatusCodes.Status200OK)
        .WithOpenApi();

        portal.MapGet("/{id:guid}", (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator,
            CancellationToken ct) => EjecutarAsync(async () => Results.Ok(await mediator.Send(new ObtenerDetalleRetiroQuery(id), ct))))
        .WithName("ObtenerRetiroDelComercio")
        .WithSummary("Detalle de un retiro del comercio")
        .Produces<RetiroDetalleDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        portal.MapPost("/{id:guid}/cancelar", (
            [FromRoute] Guid id,
            [FromBody] CancelarRetiroRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) => EjecutarAsync(async () =>
                Results.Ok(await mediator.Send(new CancelarRetiroCommand(id, request.Motivo ?? string.Empty), ct))))
        .WithName("CancelarRetiroDelComercio")
        .WithSummary("El comercio cancela un retiro que todavía no está en una hoja de ruta")
        .Produces<RetiroDetalleDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        // =====================================================================
        // API POR CLAVE: el sistema del comercio solicita el retiro
        // =====================================================================
        app.MapPost("/api/integracion/v1/retiros", async (
            [FromBody] SolicitarRetiroRequest request,
            [FromHeader(Name = "X-Api-Key")] string? claveApi,
            HttpRequest http,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var clave = !string.IsNullOrWhiteSpace(claveApi) ? claveApi : http.Headers["X-Api-Key"].FirstOrDefault();
                var basico = ACommand(request);
                var resultado = await mediator.Send(new SolicitarRetiroPorApiCommand(
                    clave, basico.Contacto, basico.Direccion, basico.Bultos, basico.Motivo,
                    basico.FranjaHorariaId, basico.FechaSolicitada, basico.ReferenciaExterna), ct);

                return resultado.Duplicado
                    ? Results.Ok(resultado)
                    : Results.Created($"/api/integracion/v1/retiros/{resultado.CodigoSeguimiento}", resultado);
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ConflictoDeConcurrenciaException)
            {
                return Results.Conflict(new { error = "Otra solicitud simultánea registró la misma referencia. Reintente: no se duplicará el retiro." });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Json(new { error = "Clave de API inválida." }, statusCode: StatusCodes.Status401Unauthorized);
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting(AltaEnvioCanalesEndpoints.PoliticaLimiteIntegracion)
        .WithTags("Integración por API · Retiros")
        .WithName("SolicitarRetiroPorApi")
        .WithSummary("Solicitud de retiro en domicilio desde el sistema del comercio")
        .WithDescription("Se autentica con la cabecera X-Api-Key. Con referenciaExterna, reintentar devuelve el retiro ya registrado (200) en vez de crear otro.")
        .Produces<SolicitarRetiroResultDto>(StatusCodes.Status201Created)
        .Produces<SolicitarRetiroResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status429TooManyRequests)
        .WithOpenApi();

        // =====================================================================
        // BACKOFFICE: el operador planifica y sigue los retiros
        // =====================================================================
        var backoffice = app.MapGroup("/api/retiros")
            .WithTags("Retiro en domicilio · Backoffice")
            .RequireAuthorization(PoliticasApi.Despacho);

        backoffice.MapGet("/", (
            [FromQuery] EstadoRetiro? estado,
            [FromServices] IMediator mediator,
            CancellationToken ct) => EjecutarAsync(async () => Results.Ok(await mediator.Send(new ListarRetirosDelOperadorQuery(estado), ct))))
        .WithName("ListarRetirosDelOperador")
        .WithSummary("Retiros del operador y conteo por estado")
        .Produces<ResumenRetirosOperadorDto>(StatusCodes.Status200OK)
        .WithOpenApi();

        backoffice.MapGet("/{id:guid}", (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator,
            CancellationToken ct) => EjecutarAsync(async () => Results.Ok(await mediator.Send(new ObtenerDetalleRetiroQuery(id), ct))))
        .WithName("ObtenerRetiroDelOperador")
        .WithSummary("Detalle e historial de un retiro")
        .Produces<RetiroDetalleDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        backoffice.MapPost("/{id:guid}/recepcion-deposito", (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator,
            CancellationToken ct) => EjecutarAsync(async () => Results.Ok(await mediator.Send(new RecibirRetiroEnDepositoCommand(id), ct))))
        .RequireAuthorization(PoliticasApi.Deposito)
        .WithName("RecibirRetiroEnDeposito")
        .WithSummary("El operario de depósito recibe los bultos retirados")
        .Produces<RetiroDetalleDto>(StatusCodes.Status200OK)
        .WithOpenApi();

        backoffice.MapPost("/{id:guid}/entrega-comercio", (
            [FromRoute] Guid id,
            [FromBody] EntregarRetiroAlComercioRequest? request,
            [FromServices] IMediator mediator,
            CancellationToken ct) => EjecutarAsync(async () =>
                Results.Ok(await mediator.Send(new EntregarRetiroAlComercioCommand(id, request?.Receptor), ct))))
        .RequireAuthorization(PoliticasApi.Deposito)
        .WithName("EntregarRetiroAlComercio")
        .WithSummary("Los bultos retirados llegan al comercio y se cierra el flujo")
        .Produces<RetiroDetalleDto>(StatusCodes.Status200OK)
        .WithOpenApi();

        var planificacion = app.MapGroup("/api/retiros")
            .WithTags("Retiro en domicilio · Planificación")
            .RequireAuthorization(PoliticasApi.Operaciones);

        planificacion.MapPost("/{id:guid}/reprogramar", (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator,
            CancellationToken ct) => EjecutarAsync(async () => Results.Ok(await mediator.Send(new ReprogramarRetiroCommand(id), ct))))
        .WithName("ReprogramarRetiro")
        .WithSummary("Tras un retiro no realizado, vuelve a solicitarse una nueva visita")
        .Produces<RetiroDetalleDto>(StatusCodes.Status200OK)
        .WithOpenApi();

        planificacion.MapPost("/{id:guid}/sacar-de-ruta", (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator,
            CancellationToken ct) => EjecutarAsync(async () => Results.Ok(await mediator.Send(new SacarRetiroDeRutaCommand(id), ct))))
        .WithName("SacarRetiroDeRuta")
        .WithSummary("Saca un retiro de una hoja de ruta que todavía no salió")
        .Produces<RetiroDetalleDto>(StatusCodes.Status200OK)
        .WithOpenApi();

        planificacion.MapPost("/{id:guid}/cancelar", (
            [FromRoute] Guid id,
            [FromBody] CancelarRetiroRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) => EjecutarAsync(async () =>
                Results.Ok(await mediator.Send(new CancelarRetiroCommand(id, request.Motivo ?? string.Empty), ct))))
        .WithName("CancelarRetiroDelOperador")
        .WithSummary("El operador cancela un retiro que todavía no está en una hoja de ruta")
        .Produces<RetiroDetalleDto>(StatusCodes.Status200OK)
        .WithOpenApi();

        // =====================================================================
        // APLICACIÓN DEL REPARTIDOR: registra el retiro en el domicilio
        // =====================================================================
        var movil = app.MapGroup("/api/mobile/v1/rutas/{hojaDeRutaId:guid}/paradas/{paradaId:guid}/retiro")
            .WithTags("Mobile · Retiro en domicilio")
            .RequireAuthorization(PoliticasApi.Repartidor);

        movil.MapPost("/", (
            [FromRoute] Guid hojaDeRutaId,
            [FromRoute] Guid paradaId,
            [FromBody] RegistrarRetiroRealizadoRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) => EjecutarAsync(async () =>
                Results.Ok(await mediator.Send(new RegistrarRetiroRealizadoCommand(
                    hojaDeRutaId, paradaId, request.RetiroId, request.IdempotencyKey,
                    request.CodigosBultosEscaneados ?? new List<string>(),
                    request.FotoEstadoBase64, request.FirmaBase64,
                    request.NombreEntrega, request.DocumentoEntrega,
                    request.Latitud, request.Longitud, request.FechaHoraDispositivoUtc), ct))))
        .WithName("RegistrarRetiroRealizado")
        .WithSummary("registrar el retiro con identificación de bultos, fotografía y firma")
        .WithDescription("Admite el envío diferido sin conectividad (clave de idempotencia). Si el servidor cambió el retiro mientras el dispositivo estaba sin conexión, prevalece lo ocurrido en el domicilio y el conflicto queda asentado.")
        .Produces<RetiroMovilResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        movil.MapPost("/no-realizado", (
            [FromRoute] Guid hojaDeRutaId,
            [FromRoute] Guid paradaId,
            [FromBody] RegistrarRetiroNoRealizadoRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) => EjecutarAsync(async () =>
                Results.Ok(await mediator.Send(new RegistrarRetiroNoRealizadoCommand(
                    hojaDeRutaId, paradaId, request.RetiroId, request.IdempotencyKey,
                    request.MotivoCodigo, request.MotivoNoEntregaId, request.Observaciones,
                    request.Latitud, request.Longitud, request.FechaHoraDispositivoUtc), ct))))
        .WithName("RegistrarRetiroNoRealizado")
        .WithSummary("Registrar que no se pudo retirar, con un motivo del catálogo del operador")
        .Produces<RetiroMovilResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .WithOpenApi();

        return app;
    }
}

public record BultoRetiroRequest(string? Descripcion, decimal Peso, decimal Alto, decimal Ancho, decimal Profundo);

public record SolicitarRetiroRequest(
    string? NombreContacto,
    string? TelefonoContacto,
    string? EmailContacto,
    string? Calle,
    string? Numero,
    string? Ciudad,
    Guid? ZonaCoberturaId,
    double? Latitud,
    double? Longitud,
    string? Referencia,
    string? Motivo,
    Guid? FranjaHorariaId,
    DateOnly? FechaSolicitada,
    string? ReferenciaExterna,
    List<BultoRetiroRequest>? Bultos);

public record CancelarRetiroRequest(string? Motivo);

public record EntregarRetiroAlComercioRequest(string? Receptor);

public record RegistrarRetiroRealizadoRequest(
    Guid RetiroId,
    string IdempotencyKey,
    IReadOnlyList<string>? CodigosBultosEscaneados,
    string? FotoEstadoBase64,
    string? FirmaBase64,
    string NombreEntrega,
    string? DocumentoEntrega = null,
    double? Latitud = null,
    double? Longitud = null,
    DateTime? FechaHoraDispositivoUtc = null);

public record RegistrarRetiroNoRealizadoRequest(
    Guid RetiroId,
    string IdempotencyKey,
    string? MotivoCodigo = null,
    Guid? MotivoNoEntregaId = null,
    string? Observaciones = null,
    double? Latitud = null,
    double? Longitud = null,
    DateTime? FechaHoraDispositivoUtc = null);
