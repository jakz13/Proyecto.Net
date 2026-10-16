using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ConfirmarCarga;
using UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ValidarEscaneoCarga;
using UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarEntregaExitosa;
using UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarIntentoFallido;
using UltimaMilla.Application.Commands.Mobile.Navegacion.SeleccionarParadaActual;
using UltimaMilla.Application.DTOs.Mobile;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Mobile.Navegacion.ObtenerSecuenciaParadas;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Api.Endpoints;

public static class MobileRutasEndpoints
{
    public static IEndpointRouteBuilder MapMobileRutasEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/mobile/v1/rutas/{hojaDeRutaId:guid}")
            .WithTags("Mobile - Operación en Calle (UC-18, UC-19, UC-20, UC-21)")
            .RequireAuthorization();

        // =========================================================================
        // UC-18: ESCANEAR BULTOS AL CARGAR EL VEHÍCULO (RF-19)
        // =========================================================================

        // A) Validar escaneo de carga (Detección de discrepancias: faltantes y sobrantes)
        group.MapPost("/carga/validar", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromBody] ValidarCargaRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new ValidarEscaneoCargaCommand(hojaDeRutaId, request.CodigosBultosEscaneados);
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
        .WithName("ValidarEscaneoCarga")
        .WithSummary("UC-18: Validar escaneo de bultos cargados")
        .WithDescription("Contrasta los bultos escaneados contra los asignados a la hoja de ruta. Reporta bultos correctos, faltantes y sobrantes.")
        .Produces<ResultadoVerificacionCargaDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // B) Confirmar carga y despachar ruta (Idempotente, inicia ruta y transiciona envíos)
        group.MapPost("/carga/confirmar", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromBody] ConfirmarCargaRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new ConfirmarCargaYDespacharRutaCommand(
                    hojaDeRutaId,
                    request.CodigosBultosCargados,
                    request.IdempotencyKey,
                    request.ObservacionesDiscrepancia);

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
        .WithName("ConfirmarCargaYDespacharRuta")
        .WithSummary("UC-18: Confirmar carga y despachar hoja de ruta")
        .WithDescription("Confirma la carga efectiva de bultos, transiciona envíos cargados a EnTransito, desasigna faltantes y despacha la ruta de manera idempotente.")
        .Produces<ConfirmarCargaResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // =========================================================================
        // UC-19: NAVEGAR ENTRE PARADAS
        // =========================================================================

        // A) Obtener secuencia ordenada de paradas para navegación
        group.MapGet("/paradas", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var query = new ObtenerSecuenciaParadasQuery(hojaDeRutaId);
                var paradas = await mediator.Send(query, ct);
                return Results.Ok(paradas);
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
        .WithName("ObtenerSecuenciaParadas")
        .WithSummary("UC-19: Obtener secuencia de paradas de la ruta")
        .WithDescription("Obtiene la lista de paradas ordenadas de la hoja de ruta en curso con datos de destino, destinatario y bultos.")
        .Produces<List<ParadaNavegacionDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // B) Seleccionar parada actual (Iniciar navegación hacia parada)
        group.MapPost("/paradas/{paradaId:guid}/seleccionar", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromRoute] Guid paradaId,
            [FromBody] SeleccionarParadaRequest? request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new SeleccionarParadaActualCommand(
                    hojaDeRutaId,
                    paradaId,
                    request?.LatitudActual,
                    request?.LongitudActual,
                    request?.Timestamp,
                    request?.Accion ?? "IniciarNavegacion");

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
        .WithName("SeleccionarParadaActual")
        .WithSummary("UC-19: Iniciar navegación hacia parada")
        .WithDescription("Marca la parada en estado EnCamino y notifica vía Outbox (SignalR en tiempo real al Backoffice).")
        .Produces<ParadaActualizadaDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // B2) Registrar arribo al destino de la parada
        group.MapPost("/paradas/{paradaId:guid}/arribo", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromRoute] Guid paradaId,
            [FromBody] RegistrarArriboRequest? request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new SeleccionarParadaActualCommand(
                    hojaDeRutaId,
                    paradaId,
                    request?.LatitudActual,
                    request?.LongitudActual,
                    request?.Timestamp,
                    Accion: "RegistrarArribo");

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
        .WithName("RegistrarArriboParada")
        .WithSummary("UC-19: Registrar arribo a parada (EnSitio)")
        .WithDescription("Marca la parada en estado EnSitio cuando el repartidor llega al domicilio del destinatario.")
        .Produces<ParadaActualizadaDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // =========================================================================
        // UC-20: REGISTRAR ENTREGA EXITOSA (RF-20, RF-11, RF-12)
        // =========================================================================
        group.MapPost("/paradas/{paradaId:guid}/entrega-exitosa", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromRoute] Guid paradaId,
            [FromBody] RegistrarEntregaExitosaRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new RegistrarEntregaExitosaCommand(
                    hojaDeRutaId,
                    paradaId,
                    request.EnvioId,
                    request.IdempotencyKey,
                    request.NombreReceptor,
                    request.DocumentoReceptor,
                    request.FirmaDigitalBase64,
                    request.FotoEvidenciaBase64,
                    request.Latitud,
                    request.Longitud,
                    request.FechaHoraDispositivoUtc);

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
        .WithName("RegistrarEntregaExitosa")
        .WithSummary("UC-20: Registrar entrega exitosa en parada")
        .WithDescription("Registra la prueba de entrega (firma, foto, documento) e intento exitoso, transiciona el envío a Entregado, completa la parada, genera EventoEnvio e integration event para webhooks y notificaciones.")
        .Produces<EntregaExitosaResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // =========================================================================
        // UC-21: REGISTRAR INTENTO DE ENTREGA FALLIDO (RF-21, RF-04, RF-11)
        // =========================================================================
        group.MapPost("/paradas/{paradaId:guid}/intento-fallido", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromRoute] Guid paradaId,
            [FromBody] RegistrarIntentoFallidoRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new RegistrarIntentoFallidoCommand(
                    hojaDeRutaId,
                    paradaId,
                    request.EnvioId,
                    request.IdempotencyKey,
                    request.MotivoNoEntregaId,
                    request.MotivoCodigo,
                    request.Observaciones,
                    request.FotoEvidenciaBase64,
                    request.Latitud,
                    request.Longitud,
                    request.FechaHoraDispositivoUtc);

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
        .WithName("RegistrarIntentoFallido")
        .WithSummary("UC-21: Registrar intento de entrega fallido en parada")
        .WithDescription("Registra incidencia con motivo del catálogo del operador. Ejecuta doble transición: 1) EnTransito a NoEntregado; 2) Evaluación contra ReglaOperativa (Reprogramado o EnDevolucion). Marca parada como fallida y notifica.")
        .Produces<ResultadoIntentoFallidoDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        return app;
    }
}

// Request Contracts
public record ValidarCargaRequest(List<string> CodigosBultosEscaneados);

public record ConfirmarCargaRequest(
    List<string> CodigosBultosCargados,
    string IdempotencyKey,
    string? ObservacionesDiscrepancia = null
);

public record SeleccionarParadaRequest(
    double? LatitudActual = null,
    double? LongitudActual = null,
    DateTime? Timestamp = null,
    string Accion = "IniciarNavegacion"
);

public record RegistrarArriboRequest(
    double? LatitudActual = null,
    double? LongitudActual = null,
    DateTime? Timestamp = null
);

public record RegistrarEntregaExitosaRequest(
    Guid EnvioId,
    string IdempotencyKey,
    string NombreReceptor,
    string DocumentoReceptor,
    string? FirmaDigitalBase64 = null,
    string? FotoEvidenciaBase64 = null,
    double? Latitud = null,
    double? Longitud = null,
    DateTime? FechaHoraDispositivoUtc = null
);

public record RegistrarIntentoFallidoRequest(
    Guid EnvioId,
    string IdempotencyKey,
    Guid? MotivoNoEntregaId = null,
    string? MotivoCodigo = null,
    string Observaciones = "",
    string? FotoEvidenciaBase64 = null,
    double? Latitud = null,
    double? Longitud = null,
    DateTime? FechaHoraDispositivoUtc = null
);
