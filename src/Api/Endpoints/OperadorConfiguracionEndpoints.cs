using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Operador.ActualizarMotivoNoEntrega;
using UltimaMilla.Application.Commands.Operador.CambiarEstadoMotivoNoEntrega;
using UltimaMilla.Application.Commands.Operador.CrearMotivoNoEntrega;
using UltimaMilla.Application.Commands.Operador.PublicarReglaOperativa;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Api.Endpoints;

public static class OperadorConfiguracionEndpoints
{
    public static IEndpointRouteBuilder MapOperadorConfiguracionEndpoints(this IEndpointRouteBuilder app)
    {
        var reglas = app.MapGroup("/api/reglas-operativas")
            .WithTags("Reglas operativas (UC-04)")
            .RequireAuthorization();

        reglas.MapGet("/", ([FromServices] IMediator m, CancellationToken ct) =>
                Ejecutar(async () => Results.Ok(await m.Send(new ListarReglasOperativasQuery(), ct))))
            .WithName("ListarReglasOperativas").WithSummary("Historial de versiones de reglas operativas")
            .Produces<IReadOnlyList<ReglaOperativaDto>>().WithOpenApi();

        reglas.MapGet("/vigente", ([FromServices] IMediator m, CancellationToken ct) =>
                Ejecutar(async () => await m.Send(new ObtenerReglaOperativaVigenteQuery(), ct) is { } r ? Results.Ok(r) : Results.NotFound()))
            .WithName("ObtenerReglaOperativaVigente").WithSummary("Versión vigente de la regla operativa")
            .Produces<ReglaOperativaDto>().Produces(StatusCodes.Status404NotFound).WithOpenApi();

        reglas.MapGet("/{id:guid}", ([FromRoute] Guid id, [FromServices] IMediator m, CancellationToken ct) =>
                Ejecutar(async () => Results.Ok(await m.Send(new ObtenerReglaOperativaQuery(id), ct))))
            .WithName("ObtenerReglaOperativa").WithSummary("Detalle de una versión de regla operativa")
            .Produces<ReglaOperativaDto>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status403Forbidden).WithOpenApi();

        reglas.MapPost("/", ([FromBody] PublicarReglaOperativaRequest r, [FromServices] IMediator m, CancellationToken ct) =>
                Ejecutar(async () =>
                {
                    var dto = await m.Send(new PublicarReglaOperativaCommand(
                        r.MaxIntentosEntrega,
                        r.ResolverPlazoEntreIntentos(),
                        r.TipoPruebaEntregaExigida,
                        r.PoliticaDevolucion,
                        r.ResolverPlazoEstandar(),
                        r.ResolverPlazoUrgente(),
                        r.ResolverMaxParadas(),
                        r.Comentario), ct);
                    return Results.Created($"/api/reglas-operativas/{dto.Id}", dto);
                }))
            .WithName("PublicarReglaOperativa").WithSummary("Publicar una nueva versión de regla operativa (UC-04)")
            .Produces<ReglaOperativaDto>(StatusCodes.Status201Created).Produces(StatusCodes.Status400BadRequest).WithOpenApi();

        reglas.MapPost("/simular", ([FromBody] SimularReglaOperativaRequest r, [FromServices] IMediator m, CancellationToken ct) =>
                Ejecutar(async () => Results.Ok(await m.Send(new SimularReglaOperativaQuery(
                    r.ModalidadServicio ?? "Estándar",
                    r.FechaDespacho == default ? DateTime.UtcNow : r.FechaDespacho,
                    r.IntentosRealizados,
                    r.FechaUltimoIntento), ct))))
            .WithName("SimularReglasOperativas").WithSummary("Simular cumplimiento de reglas operativas para un envío (UC-04)")
            .Produces<ResultadoSimulacionReglasDto>().WithOpenApi();

        var motivos = app.MapGroup("/api/v1/motivos-no-entrega")
            .WithTags("Motivos de no entrega (UC-05)")
            .RequireAuthorization();

        motivos.MapGet("/", ([FromQuery] string? busqueda, [FromQuery] bool? soloActivos, [FromServices] IMediator m, CancellationToken ct) =>
                Ejecutar(async () => Results.Ok(await m.Send(new ListarMotivosNoEntregaQuery(busqueda, soloActivos), ct))))
            .WithName("ListarMotivosNoEntrega").WithSummary("Catálogo de motivos de no entrega")
            .Produces<IReadOnlyList<MotivoNoEntregaDto>>().WithOpenApi();

        motivos.MapGet("/resumen", ([FromServices] IMediator m, CancellationToken ct) =>
                Ejecutar(async () => Results.Ok(await m.Send(new ObtenerResumenMotivosNoEntregaQuery(), ct))))
            .WithName("ObtenerResumenMotivosNoEntrega").WithSummary("Resumen de métricas de motivos de no entrega")
            .Produces<ResumenMotivosNoEntregaDto>().WithOpenApi();

        motivos.MapGet("/sincronizacion-movil", ([FromServices] IMediator m, CancellationToken ct) =>
                Ejecutar(async () => Results.Ok(await m.Send(new ListarMotivosParaRepartidorQuery(), ct))))
            .WithName("ListarMotivosParaRepartidor").WithSummary("Motivos activos no internos para la app del repartidor")
            .Produces<IReadOnlyList<MotivoNoEntregaDto>>().WithOpenApi();

        motivos.MapGet("/{id:guid}", ([FromRoute] Guid id, [FromServices] IMediator m, CancellationToken ct) =>
                Ejecutar(async () => Results.Ok(await m.Send(new ObtenerMotivoNoEntregaQuery(id), ct))))
            .WithName("ObtenerMotivoNoEntrega").WithSummary("Detalle de un motivo")
            .Produces<MotivoNoEntregaDto>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status403Forbidden).WithOpenApi();

        motivos.MapPost("/", ([FromBody] CrearMotivoNoEntregaRequest r, [FromServices] IMediator m, CancellationToken ct) =>
                Ejecutar(async () =>
                {
                    var dto = await m.Send(new CrearMotivoNoEntregaCommand(r.Codigo, r.Descripcion, r.EsInterno), ct);
                    return Results.Created($"/api/v1/motivos-no-entrega/{dto.Id}", dto);
                }))
            .WithName("CrearMotivoNoEntrega").WithSummary("Crear un motivo de no entrega (UC-05)")
            .Produces<MotivoNoEntregaDto>(StatusCodes.Status201Created).Produces(StatusCodes.Status400BadRequest).WithOpenApi();

        motivos.MapPut("/{id:guid}", ([FromRoute] Guid id, [FromBody] ActualizarMotivoNoEntregaRequest r, [FromServices] IMediator m, CancellationToken ct) =>
                Ejecutar(async () => Results.Ok(await m.Send(new ActualizarMotivoNoEntregaCommand(id, r.Descripcion, r.Activo), ct))))
            .WithName("ActualizarMotivoNoEntrega").WithSummary("Editar la descripción o estado de un motivo")
            .Produces<MotivoNoEntregaDto>().Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden).WithOpenApi();

        motivos.MapPatch("/{id:guid}/estado", ([FromRoute] Guid id, [FromBody] CambiarEstadoMotivoNoEntregaRequest r, [FromServices] IMediator m, CancellationToken ct) =>
                Ejecutar(async () => Results.Ok(await m.Send(new CambiarEstadoMotivoNoEntregaCommand(id, r.Activo), ct))))
            .WithName("CambiarEstadoMotivoNoEntrega").WithSummary("Activar o desactivar un motivo")
            .Produces<MotivoNoEntregaDto>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status403Forbidden).WithOpenApi();

        return app;
    }

    private static async Task<IResult> Ejecutar(Func<Task<IResult>> accion)
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
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
    }
}

public record PublicarReglaOperativaRequest(
    int MaxIntentosEntrega,
    TimeSpan? PlazoEntreIntentos = null,
    string? TipoPruebaEntregaExigida = null,
    string? PoliticaDevolucion = null,
    TimeSpan? PlazoComprometidoEstandar = null,
    TimeSpan? PlazoComprometidoUrgente = null,
    int? MaxParadasPorRuta = null,
    int? HorasEntreIntentos = null,
    int? HorasPlazoComprometidoEstandar = null,
    int? HorasPlazoComprometidoUrgente = null,
    string? Comentario = null)
{
    public TimeSpan ResolverPlazoEntreIntentos() =>
        PlazoEntreIntentos ?? (HorasEntreIntentos.HasValue ? TimeSpan.FromHours(HorasEntreIntentos.Value) : TimeSpan.FromHours(24));

    public TimeSpan ResolverPlazoEstandar() =>
        PlazoComprometidoEstandar ?? (HorasPlazoComprometidoEstandar.HasValue ? TimeSpan.FromHours(HorasPlazoComprometidoEstandar.Value) : TimeSpan.FromHours(48));

    public TimeSpan ResolverPlazoUrgente() =>
        PlazoComprometidoUrgente ?? (HorasPlazoComprometidoUrgente.HasValue ? TimeSpan.FromHours(HorasPlazoComprometidoUrgente.Value) : TimeSpan.FromHours(12));

    public int ResolverMaxParadas() =>
        MaxParadasPorRuta is > 0 ? MaxParadasPorRuta.Value : 50;
}

public record SimularReglaOperativaRequest(
    string? ModalidadServicio,
    DateTime FechaDespacho,
    int IntentosRealizados,
    DateTime? FechaUltimoIntento);

public record CrearMotivoNoEntregaRequest(string? Codigo, string? Descripcion, bool EsInterno = false);

public record ActualizarMotivoNoEntregaRequest(string? Descripcion, bool? Activo = null);

public record CambiarEstadoMotivoNoEntregaRequest(bool Activo);
