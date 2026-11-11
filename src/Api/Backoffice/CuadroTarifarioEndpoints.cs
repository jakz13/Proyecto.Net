using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Operador.GestionTarifas;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Queries.Operador.GestionTarifas;
using Contratos = UltimaMilla.Frontend.Contracts.Modelos.Tarifas;
using UltimaMilla.Api.Auth;

namespace UltimaMilla.Api.Backoffice;

/// <summary>
/// Endpoints para la gestión versionada del cuadro tarifario y simulación de cotizaciones (UC-03, RF-03).
/// Conecta la Minimal API con la capa de Aplicación a través de MediatR.
/// </summary>
public static class CuadroTarifarioEndpoints
{
    public static IEndpointRouteBuilder MapCuadroTarifarioEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/cuadros-tarifarios")
            .WithTags("Backoffice · Cuadro tarifario")
            .RequireAuthorization(PoliticasApi.Administracion)
            .ExigirParaEscritura(PoliticasApi.Administracion);

        // 1. Listado del historial completo de versiones del cuadro tarifario
        grupo.MapGet("/", (IMediator mediator, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var resultado = await mediator.Send(new ListarVersionesCuadroTarifarioQuery(), ct);
            var contratosDto = resultado.Select(MapearAContrato).ToList();
            return Results.Ok(contratosDto);
        }))
        .WithName("ListarCuadrosTarifarios")
        .WithSummary("Historial de versiones del cuadro tarifario");

        // 2. Consulta de la versión vigente actual
        grupo.MapGet("/vigente", (IMediator mediator, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var vigente = await mediator.Send(new ObtenerCuadroTarifarioVigenteQuery(), ct);
            if (vigente is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No hay un cuadro tarifario vigente.");

            return Results.Ok(MapearAContrato(vigente));
        }))
        .WithName("ObtenerCuadroTarifarioVigente")
        .WithSummary("Versión vigente del cuadro tarifario");

        // 3. Consulta de una versión puntual por ID (con Anti-IDOR en el handler)
        grupo.MapGet("/{id:guid}", (Guid id, IMediator mediator, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var cuadro = await mediator.Send(new ObtenerCuadroTarifarioPorIdQuery(id), ct);
            return Results.Ok(MapearAContrato(cuadro));
        }))
        .WithName("ObtenerCuadroTarifario")
        .WithSummary("Detalle de una versión del cuadro tarifario");

        // 4. Publicar nueva versión del cuadro tarifario
        grupo.MapPost("/", (
            [FromBody] Contratos.CrearVersionCuadroTarifarioDto dto,
            IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var reglasDto = (dto.ReglasTarifa ?? new List<Contratos.CrearReglaTarifaDto>())
                .Select(r => new CrearReglaTarifaDto(
                    r.ZonaCoberturaId,
                    r.ModalidadServicio,
                    r.PesoDesde,
                    r.PesoHasta,
                    r.VolumenDesde,
                    r.VolumenHasta,
                    r.PrecioBase,
                    r.Recargo,
                    r.Bonificacion))
                .ToList();

            var comando = new PublicarVersionCuadroTarifarioCommand(
                dto.VigenteDesde,
                dto.Comentario,
                reglasDto);

            var nuevoCuadro = await mediator.Send(comando, ct);
            return Results.Created($"/api/cuadros-tarifarios/{nuevoCuadro.Id}", MapearAContrato(nuevoCuadro));
        }))
        .WithName("PublicarCuadroTarifario")
        .WithSummary("Publicar una nueva versión del cuadro tarifario");

        // 5. Simular cotización tarifaria bajo la versión vigente
        grupo.MapPost("/simular", (
            [FromBody] Contratos.SimularTarifaDto dto,
            IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var comando = new SimularCotizacionTarifaCommand(
                dto.PesoKg,
                dto.VolumenM3,
                dto.ModalidadServicio,
                dto.ZonaCoberturaId);

            var resultado = await mediator.Send(comando, ct);
            return Results.Ok(new Contratos.CotizacionSimuladaDto
            {
                Exitoso = resultado.Exitoso,
                PrecioFinal = resultado.PrecioFinal,
                PrecioBase = resultado.PrecioBase,
                Recargo = resultado.Recargo,
                Bonificacion = resultado.Bonificacion,
                VersionCuadroAplicada = resultado.VersionCuadroAplicada,
                ReglaAplicada = resultado.ReglaAplicada,
                Mensaje = resultado.Mensaje
            });
        }))
        .WithName("SimularTarifa")
        .WithSummary("Cotizar un envío con la versión vigente del cuadro");

        return app;
    }

    private static Contratos.CuadroTarifarioDto MapearAContrato(CuadroTarifarioDetalleDto c) => new()
    {
        Id = c.Id,
        OperadorId = c.OperadorId,
        Version = c.Version,
        VigenteDesde = c.VigenteDesde,
        VigenteHasta = c.VigenteHasta,
        Comentario = c.Comentario,
        ReglasTarifa = c.ReglasTarifa.Select(r => new Contratos.ReglaTarifaDto
        {
            Id = r.Id,
            CuadroTarifarioId = r.CuadroTarifarioId,
            ZonaCoberturaId = r.ZonaCoberturaId,
            NombreZona = r.NombreZona,
            ModalidadServicio = r.ModalidadServicio,
            PesoDesde = r.PesoDesde,
            PesoHasta = r.PesoHasta,
            VolumenDesde = r.VolumenDesde,
            VolumenHasta = r.VolumenHasta,
            PrecioBase = r.PrecioBase,
            Recargo = r.Recargo,
            Bonificacion = r.Bonificacion
        }).ToList()
    };
}
