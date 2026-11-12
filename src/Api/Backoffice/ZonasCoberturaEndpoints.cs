using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Operador.GestionZonas;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Queries.Operador.GestionZonas;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;
using UltimaMilla.Api.Auth;

namespace UltimaMilla.Api.Backoffice;

/// <summary>
/// Endpoints para el caso de uso Zonas de cobertura y franjas horarias de entrega.
/// </summary>
public static class ZonasCoberturaEndpoints
{
    public static IEndpointRouteBuilder MapZonasCoberturaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/zonas-cobertura")
            .WithTags("Backoffice · Zonas y franjas")
            .RequireAuthorization(PoliticasApi.PersonalDelOperador)
            .ExigirParaEscritura(PoliticasApi.Administracion);

        grupo.MapGet("/", (
            [FromQuery] string? q,
            [FromQuery] bool? soloActivas,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var zonas = await mediator.Send(new ListarZonasCoberturaQuery(q, soloActivas), ct);
            return Results.Ok(zonas.Select(Mapear).ToList());
        }))
        .WithName("ListarZonasCobertura")
        .WithSummary("Listar las zonas de cobertura con sus franjas")
        .Produces<List<ZonaCoberturaDto>>();

        grupo.MapGet("/{id:guid}", (
            Guid id,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var zona = await mediator.Send(new ObtenerZonaCoberturaPorIdQuery(id), ct);
            return Results.Ok(Mapear(zona));
        }))
        .WithName("ObtenerZonaCobertura")
        .WithSummary("Detalle de una zona con sus franjas horarias")
        .Produces<ZonaCoberturaDto>()
        .Produces(StatusCodes.Status404NotFound);

        grupo.MapPost("/", (
            [FromBody] CrearZonaCoberturaDto dto,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var franjasDto = dto.FranjasHorarias?.Select(f => new Application.DTOs.Operador.CrearFranjaHorariaDto(
                f.DiaSemana,
                f.HoraInicio,
                f.HoraFin,
                f.CapacidadMaximaEnvios
            )).ToList();

            var comando = new CrearZonaCoberturaCommand(
                dto.Nombre,
                dto.PoligonoGeografico,
                dto.ColorHex,
                franjasDto
            );

            var creada = await mediator.Send(comando, ct);
            return Results.Created($"/api/zonas-cobertura/{creada.Id}", Mapear(creada));
        }))
        .WithName("CrearZonaCobertura")
        .WithSummary("Definir una zona de cobertura y sus franjas horarias")
        .Produces<ZonaCoberturaDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        grupo.MapPut("/{id:guid}", (
            Guid id,
            [FromBody] ActualizarZonaCoberturaDto dto,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var franjasDto = dto.FranjasHorarias?.Select(f => new Application.DTOs.Operador.CrearFranjaHorariaDto(
                f.DiaSemana,
                f.HoraInicio,
                f.HoraFin,
                f.CapacidadMaximaEnvios
            )).ToList();

            var comando = new ActualizarZonaCoberturaCommand(
                id,
                dto.Nombre,
                dto.PoligonoGeografico,
                dto.ColorHex,
                dto.Activa,
                franjasDto
            );

            var actualizada = await mediator.Send(comando, ct);
            return Results.Ok(Mapear(actualizada));
        }))
        .WithName("ActualizarZonaCobertura")
        .WithSummary("Modificar el polígono y las franjas de una zona")
        .Produces<ZonaCoberturaDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        grupo.MapPatch("/{id:guid}/estado", (
            Guid id,
            [FromBody] CambioEstadoActivaRequest cuerpo,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            await mediator.Send(new CambiarEstadoZonaCoberturaCommand(id, cuerpo.Activa), ct);
            return Results.NoContent();
        }))
        .WithName("CambiarEstadoZonaCobertura")
        .WithSummary("Dar de alta o baja lógica a una zona")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        grupo.MapPost("/validar-franjas", (
            [FromBody] List<Frontend.Contracts.Modelos.Zonas.CrearFranjaHorariaDto> franjas,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var franjasDto = franjas?.Select(f => new Application.DTOs.Operador.CrearFranjaHorariaDto(
                f.DiaSemana,
                f.HoraInicio,
                f.HoraFin,
                f.CapacidadMaximaEnvios
            )).ToList();

            await mediator.Send(new ValidarFranjasHorariasCommand(franjasDto), ct);
            return Results.Ok();
        }))
        .WithName("ValidarFranjasHorarias")
        .WithSummary("Detectar franjas horarias que se superponen en un mismo día")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        return app;
    }

    private static ZonaCoberturaDto Mapear(ZonaCoberturaDetalleDto detalle) => new()
    {
        Id = detalle.Id,
        OperadorId = detalle.OperadorId,
        Nombre = detalle.Nombre,
        PoligonoGeografico = detalle.PoligonoGeografico,
        ColorHex = detalle.ColorHex,
        Activa = detalle.Activa,
        FechaCreacion = detalle.FechaCreacion,
        FranjasHorarias = detalle.FranjasHorarias.Select(f => new FranjaHorariaDto
        {
            Id = f.Id,
            ZonaCoberturaId = f.ZonaCoberturaId,
            DiaSemana = f.DiaSemana,
            HoraInicio = f.HoraInicio,
            HoraFin = f.HoraFin,
            CapacidadMaximaEnvios = f.CapacidadMaximaEnvios,
            Activa = f.Activa
        }).ToList()
    };
}
