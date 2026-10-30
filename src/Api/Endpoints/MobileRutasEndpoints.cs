using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ConfirmarCarga;
using UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ValidarEscaneoCarga;
using UltimaMilla.Application.Commands.Mobile.DevolucionEnCalle.RegistrarRetiroDevolucion;
using UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarEntregaExitosa;
using UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarIntentoFallido;
using UltimaMilla.Application.Commands.Mobile.Navegacion.RegistrarEventoGeocerca;
using UltimaMilla.Application.Commands.Mobile.Navegacion.SeleccionarParadaActual;
using UltimaMilla.Application.DTOs.Mobile;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Mobile.DescargarHojasDeRuta;
using UltimaMilla.Application.Queries.Mobile.Navegacion.ObtenerSecuenciaParadas;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Endpoints;

public static class MobileRutasEndpoints
{
    public static IEndpointRouteBuilder MapMobileRutasEndpoints(this IEndpointRouteBuilder app)
    {
        // =========================================================================
        // UC-17: DESCARGAR HOJAS DE RUTA DE LA JORNADA (RF-18)
        // =========================================================================
        app.MapGet("/api/mobile/v1/rutas", async (
            [FromQuery] DateOnly? fecha,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var resultado = await mediator.Send(new DescargarHojasDeRutaQuery(fecha), ct);
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
        .WithTags("Mobile - Descarga de hoja de ruta (UC-17)")
        .RequireAuthorization()
        .WithName("DescargarHojasDeRuta")
        .WithSummary("UC-17: Descargar las hojas de ruta de la jornada")
        .WithDescription("Devuelve las hojas de ruta liberadas del repartidor autenticado para la fecha indicada (por defecto hoy), con paradas, destinatarios, bultos y la configuración necesaria para operar sin conexión.")
        .Produces<DescargaHojasDeRutaDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

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
                var command = new ConfirmarCargaCommand(
                    hojaDeRutaId,
                    request.CodigosBultosCargados,
                    request.IdempotencyKey);

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
        .WithName("ConfirmarCarga")
        .WithSummary("UC-18: Confirmar la carga del vehículo")
        .WithDescription("Registra qué bultos se cargaron efectivamente en el vehículo y deja la ruta lista para que el operario la despache (UC-09). No transiciona envíos.")
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

        // =========================================================================
        // UC-22: GESTIONAR DEVOLUCIÓN EN LA CALLE (RF-22)
        // =========================================================================
        group.MapPost("/paradas/{paradaId:guid}/retiro-devolucion", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromRoute] Guid paradaId,
            [FromBody] RegistrarRetiroDevolucionRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new RegistrarRetiroDevolucionCommand(
                    hojaDeRutaId,
                    paradaId,
                    request.EnvioId,
                    request.IdempotencyKey,
                    request.Latitud,
                    request.Longitud,
                    request.FechaHoraDispositivoUtc,
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
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        })
        .WithName("RegistrarRetiroDevolucion")
        .WithSummary("UC-22: Registrar el retiro del bulto para devolverlo al depósito")
        .WithDescription("Para un envío en devolución, marca sus bultos en retorno al depósito y registra la devolución como en camino. Es idempotente.")
        .Produces<RetiroDevolucionResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // =========================================================================
        // =========================================================================
        // GEOCERCAS: DETECCIÓN AUTOMÁTICA DE LLEGADA Y SALIDA (Sección 7.3)
        // =========================================================================
        group.MapPost("/paradas/{paradaId:guid}/geocerca", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromRoute] Guid paradaId,
            [FromBody] RegistrarEventoGeocercaRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new RegistrarEventoGeocercaCommand(
                    hojaDeRutaId,
                    paradaId,
                    request.EnvioId,
                    request.TipoEvento,
                    request.Latitud,
                    request.Longitud,
                    request.DistanciaMetros,
                    request.TiempoPermanenciaSegundos,
                    request.TimestampUtc,
                    request.IdempotencyKey);

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
        .WithName("RegistrarEventoGeocerca")
        .WithSummary("Sección 7.3: Registrar evento automático de llegada o salida de geocerca")
        .WithDescription("Registra en el envío el evento LLEGADA_GEOCERCA (< 100m) o SALIDA_GEOCERCA (> 150m) con histéresis e invalida la caché de seguimiento público. Es idempotente.")
        .Produces<EventoGeocercaResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // REINICIO DE DATOS DE DEMO (MODO OFFLINE/ONLINE)
        // =========================================================================
        app.MapPost("/api/mobile/v1/demo/reset", async (
            [FromServices] AppDbContext dbContext,
            [FromServices] IDistributedCacheService cacheService,
            CancellationToken ct) =>
        {
            var hojaDeRutaId = DatabaseSeeder.DefaultHojaDeRutaId;
            var ruta = await dbContext.HojasDeRuta
                .Include(h => h.Paradas)
                .FirstOrDefaultAsync(h => h.Id == hojaDeRutaId, ct);

            if (ruta != null)
            {
                ruta.ReiniciarParaDemo();
            }

            var id1 = DatabaseSeeder.Envio1Id;
            var id2 = DatabaseSeeder.Envio2Id;
            var id3 = DatabaseSeeder.Envio3Id;

            var envios = await dbContext.Envios
                .Include(e => e.Bultos)
                .Include(e => e.Eventos)
                .Include(e => e.IntentosEntrega)
                .Where(e => e.Id == id1 || e.Id == id2 || e.Id == id3)
                .ToListAsync(ct);

            var intentos = await dbContext.IntentosEntrega
                .Where(i => i.EnvioId == id1 || i.EnvioId == id2 || i.EnvioId == id3)
                .ToListAsync(ct);

            if (intentos.Count > 0)
            {
                var pruebas = await dbContext.PruebasDeEntrega
                    .Where(p => dbContext.IntentosEntrega
                        .Where(i => i.EnvioId == id1 || i.EnvioId == id2 || i.EnvioId == id3)
                        .Select(i => i.Id)
                        .Contains(p.IntentoEntregaId))
                    .ToListAsync(ct);

                dbContext.PruebasDeEntrega.RemoveRange(pruebas);
                dbContext.IntentosEntrega.RemoveRange(intentos);
            }

            var eventosGeocercaODemo = await dbContext.EventosEnvio
                .Where(ev => (ev.EnvioId == id1 || ev.EnvioId == id2 || ev.EnvioId == id3)
                    && (ev.TipoEvento == "LlegadaGeocerca" || ev.TipoEvento == "SalidaGeocerca" || ev.TipoEvento == "ReinicioDemo"))
                .ToListAsync(ct);

            if (eventosGeocercaODemo.Count > 0)
            {
                dbContext.EventosEnvio.RemoveRange(eventosGeocercaODemo);
            }

            foreach (var envio in envios)
            {
                envio.ReiniciarParaDemoEnTransito(hojaDeRutaId);
                await cacheService.RemoveAsync($"seguimiento:{envio.CodigoSeguimientoPublico}", ct);
                await cacheService.RemoveAsync($"envio:{envio.Id}", ct);
            }

            await dbContext.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                mensaje = "Demo reiniciada con éxito en PostgreSQL. ENV-2026-00108, ENV-2026-00109 y ENV-2026-00110 restaurados a EnTransito (4).",
                hojaDeRutaId,
                enviosReiniciados = envios.Select(e => e.CodigoSeguimientoPublico)
            });
        })
        .AllowAnonymous()
        .WithName("ReiniciarDemoCalle")
        .WithSummary("Reinicia los datos demo de operación en calle a estado EnTransito (4)")
        .WithTags("Mobile - Demo");

        return app;
    }
}

public record RegistrarEventoGeocercaRequest(
    Guid EnvioId,
    string TipoEvento,
    double Latitud,
    double Longitud,
    double DistanciaMetros,
    int? TiempoPermanenciaSegundos = null,
    DateTime? TimestampUtc = null,
    string? IdempotencyKey = null);

public record RegistrarRetiroDevolucionRequest(
    Guid EnvioId,
    string IdempotencyKey,
    double? Latitud = null,
    double? Longitud = null,
    DateTime? FechaHoraDispositivoUtc = null,
    string? Observaciones = null);

// Request Contracts
public record ValidarCargaRequest(List<string> CodigosBultosEscaneados);

public record ConfirmarCargaRequest(
    List<string> CodigosBultosCargados,
    string IdempotencyKey
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
