using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Operador.GestionComercios;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Queries.Operador.GestionComercios;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Comercios;

namespace UltimaMilla.Api.Backoffice;

/// <summary>
/// Endpoints para el caso de uso UC-01: Gestión de comercios clientes del operador.
/// </summary>
public static class ComerciosEndpoints
{
    public static IEndpointRouteBuilder MapComerciosBackofficeEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/comercios")
            .WithTags("Backoffice · Comercios (UC-01)")
            .RequireAuthorization();

        grupo.MapGet("/", (
            [FromQuery] int? pagina,
            [FromQuery] int? tamanoPagina,
            [FromQuery] string? q,
            [FromQuery] bool? soloActivos,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var (paginaActual, tamano) = RespuestasBackoffice.NormalizarPagina(pagina, tamanoPagina);
            var resultado = await mediator.Send(new ListarComerciosQuery(q, soloActivos, paginaActual, tamano), ct);

            return Results.Ok(new ResultadoPaginadoDto<ComercioDto>
            {
                Elementos = resultado.Elementos.Select(Mapear).ToList(),
                TotalRegistros = resultado.TotalRegistros,
                PaginaActual = resultado.PaginaActual,
                TamanoPagina = resultado.TamanoPagina
            });
        }))
        .WithName("ListarComercios")
        .WithSummary("Listar los comercios del operador con búsqueda y paginación")
        .Produces<ResultadoPaginadoDto<ComercioDto>>();

        grupo.MapGet("/{id:guid}", (
            Guid id,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var detalle = await mediator.Send(new ObtenerComercioPorIdQuery(id), ct);
            return Results.Ok(Mapear(detalle));
        }))
        .WithName("ObtenerComercio")
        .WithSummary("Detalle de un comercio")
        .Produces<ComercioDto>()
        .Produces(StatusCodes.Status404NotFound);

        grupo.MapPost("/", (
            [FromBody] CrearComercioDto dto,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var comando = new CrearComercioCommand(
                dto.RazonSocial,
                dto.NombreFantasia,
                dto.Rut,
                dto.Email,
                dto.Telefono,
                dto.Direccion
            );

            var creado = await mediator.Send(comando, ct);
            return Results.Created($"/api/comercios/{creado.Id}", Mapear(creado));
        }))
        .WithName("CrearComercio")
        .WithSummary("Dar de alta un comercio cliente")
        .Produces<ComercioDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        grupo.MapPut("/{id:guid}", (
            Guid id,
            [FromBody] ActualizarComercioDto dto,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var comando = new ActualizarComercioCommand(
                id,
                dto.RazonSocial,
                dto.NombreFantasia,
                dto.Email,
                dto.Telefono,
                dto.Direccion,
                dto.Activo,
                dto.SelloModificacion
            );

            var actualizado = await mediator.Send(comando, ct);
            return Results.Ok(Mapear(actualizado));
        }))
        .WithName("ActualizarComercio")
        .WithSummary("Modificar los datos de un comercio")
        .Produces<ComercioDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        grupo.MapPatch("/{id:guid}/estado", (
            Guid id,
            [FromBody] CambioEstadoActivoRequest cuerpo,
            [FromServices] IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            await mediator.Send(new CambiarEstadoComercioCommand(id, cuerpo.Activo), ct);
            return Results.NoContent();
        }))
        .WithName("CambiarEstadoComercio")
        .WithSummary("Dar de alta o baja lógica a un comercio")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static ComercioDto Mapear(ComercioDetalleDto detalle) => new()
    {
        Id = detalle.Id,
        RazonSocial = detalle.RazonSocial,
        NombreFantasia = detalle.NombreFantasia,
        Rut = detalle.Rut,
        Email = detalle.Email,
        Telefono = detalle.Telefono,
        Direccion = detalle.Direccion,
        Activo = detalle.Activo,
        FechaAlta = detalle.FechaAlta,
        SelloModificacion = detalle.SelloModificacion
    };
}
