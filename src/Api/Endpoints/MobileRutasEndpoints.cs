using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Commands.Deposito.RendirHojaDeRuta;
using UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ConfirmarCarga;
using UltimaMilla.Application.Commands.Mobile.CargaVehiculo.IniciarRecorrido;
using UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ValidarEscaneoCarga;
using UltimaMilla.Application.Commands.Mobile.DevolucionEnCalle.RegistrarRetiroDevolucion;
using UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarEntregaExitosa;
using UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarEntregaParcial;
using UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarIntentoFallido;
using UltimaMilla.Application.Commands.Mobile.Navegacion.RegistrarEventoGeocerca;
using UltimaMilla.Application.Commands.Mobile.Navegacion.SeleccionarParadaActual;
using UltimaMilla.Application.DTOs.Mobile;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Mobile.DescargarHojasDeRuta;
using UltimaMilla.Application.Queries.Mobile.Navegacion.ObtenerSecuenciaParadas;
using UltimaMilla.Application.Queries.Mobile.ObtenerRutaActivaRepartidor;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Api.Auth;

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
        .RequireAuthorization(PoliticasApi.Repartidor)
        .WithName("DescargarHojasDeRuta")
        .WithSummary("UC-17: Descargar las hojas de ruta de la jornada")
        .WithDescription("Devuelve las hojas de ruta liberadas del repartidor autenticado para la fecha indicada (por defecto hoy), con paradas, destinatarios, bultos y la configuración necesaria para operar sin conexión.")
        .Produces<DescargaHojasDeRutaDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // =========================================================================
        // OBTENER HOJA DE RUTA ACTIVA DEL REPARTIDOR AUTENTICADO
        // =========================================================================
        var obtenerRutaActivaHandler = async (
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var resultado = await mediator.Send(new ObtenerRutaActivaRepartidorQuery(), ct);
                return Results.Ok(resultado);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        };

        app.MapGet("/api/mobile/v1/rutas/activa", obtenerRutaActivaHandler)
            .WithTags("Mobile - Descarga de hoja de ruta (UC-17)")
            .RequireAuthorization(PoliticasApi.Repartidor)
            .WithName("ObtenerRutaActivaRepartidor")
            .WithSummary("Obtener la hoja de ruta activa del repartidor autenticado")
            .WithDescription("Resuelve el RepartidorId a partir de los Claims del JWT y devuelve su hoja de ruta en estado Despachada o EnTransito junto con sus paradas ordenadas, bultos y destinatarios. Retorna 404 si no posee ruta activa.")
            .Produces<RutaActivaRepartidorDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .WithOpenApi();

        app.MapGet("/api/mobile/v1/rutas/activas", async (
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var resultado = await mediator.Send(new ObtenerRutasActivasRepartidorQuery(), ct);
                return Results.Ok(resultado);
            }
            catch (NotFoundException)
            {
                return Results.Ok(new List<RutaActivaRepartidorDto>());
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
        .RequireAuthorization(PoliticasApi.Repartidor)
        .WithName("ObtenerRutasActivasRepartidor")
        .WithSummary("Obtener todas las hojas de ruta vigentes del repartidor autenticado")
        .WithDescription("Devuelve todas las hojas de ruta vigentes (Liberada, EnCarga, Despachada o EnTransito) del repartidor junto con paradas y bultos.")
        .Produces<List<RutaActivaRepartidorDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        app.MapGet("/api/mobile/v1/rutas/mi-ruta-activa", obtenerRutaActivaHandler)
            .WithTags("Mobile - Descarga de hoja de ruta (UC-17)")
            .RequireAuthorization(PoliticasApi.Repartidor)
            .ExcludeFromDescription();

        var group = app.MapGroup("/api/mobile/v1/rutas/{hojaDeRutaId:guid}")
            .WithTags("Mobile - Operación en Calle (UC-18, UC-19, UC-20, UC-21)")
            .RequireAuthorization(PoliticasApi.Repartidor)
            .AddEndpointFilter(async (contexto, siguiente) =>
            {
                if (HttpMethods.IsPost(contexto.HttpContext.Request.Method))
                {
                    contexto.HttpContext.RequestServices.GetService<IMetricasNegocio>()?.RegistrarEventoSincronizacion(1);
                }

                return await siguiente(contexto);
            });

        // =========================================================================
        // FINALIZAR HOJA DE RUTA / RENDICIÓN POR RETORNO A DEPÓSITO
        // =========================================================================
        var finalizarRutaHandler = async (
            [FromRoute] Guid hojaDeRutaId,
            [FromBody] FinalizarRutaMobileRequest? request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new RendirHojaDeRutaCommand(
                    hojaDeRutaId,
                    request?.CodigosBultosDevueltos ?? new List<string>(),
                    request?.MotivoInternoCodigo,
                    request?.Observaciones ?? "Finalización de hoja de ruta por retorno a depósito desde la aplicación móvil.");

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
            catch (ConflictoDeConcurrenciaException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        };

        group.MapPost("/finalizar", finalizarRutaHandler)
            .WithName("FinalizarHojaDeRuta")
            .WithSummary("Finalizar hoja de ruta al completar paradas o retornar a depósito")
            .WithDescription("Rinde y cierra la hoja de ruta desde la app móvil, liberando al repartidor para asumir un nuevo recorrido.")
            .Produces<RendicionResultDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithOpenApi();

        group.MapPost("/rendicion", finalizarRutaHandler)
            .WithName("RendirHojaDeRutaMobile")
            .ExcludeFromDescription();

        // =========================================================================
        // UC-18: ESCANEAR BULTOS AL CARGAR EL VEHÍCULO (RF-19)
        // =========================================================================

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

        // C) Iniciar recorrido (Transición de Despachada a EnTransito)
        group.MapPost("/iniciar-recorrido", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromBody] IniciarRecorridoRequest? request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new IniciarRecorridoCommand(hojaDeRutaId, request?.IdempotencyKey);
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
        .WithName("IniciarRecorrido")
        .WithSummary("UC-18 / UC-19: Iniciar recorrido en calle de la hoja de ruta")
        .WithDescription("Pasa la hoja de ruta de Despachada a EnTransito, emite el evento de inicio hacia el Outbox y habilita la confirmación de entregas.")
        .Produces<IniciarRecorridoResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        // =========================================================================
        // UC-19: NAVEGAR ENTRE PARADAS
        // =========================================================================

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
                    request.FechaHoraDispositivoUtc,
                    request.CodigosBultosEntregados,
                    request.BultosNoEntregados);

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
        .Produces(StatusCodes.Status409Conflict)
        .WithOpenApi();

        // =========================================================================
        // CAMBIO 2.1: REGISTRAR ENTREGA PARCIAL POR BULTOS (RF-31, RF-32, RF-33, RF-34)
        // =========================================================================
        group.MapPost("/paradas/{paradaId:guid}/entrega-parcial", async (
            [FromRoute] Guid hojaDeRutaId,
            [FromRoute] Guid paradaId,
            [FromBody] RegistrarEntregaParcialRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new RegistrarEntregaParcialCommand(
                    hojaDeRutaId,
                    paradaId,
                    request.EnvioId,
                    request.IdempotencyKey,
                    request.CodigosBultosEntregados ?? Array.Empty<string>(),
                    request.BultosNoEntregados ?? Array.Empty<BultoNoEntregadoRequestDto>(),
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
        .WithName("RegistrarEntregaParcial")
        .WithSummary("RF-31 a RF-34: Registrar entrega parcial de bultos en parada")
        .WithDescription("Registra mediante escaneo qué bultos se entregan y el motivo de no entrega para los restantes, asocia la prueba de entrega a los bultos entregados y deriva el estado del envío.")
        .Produces<EntregaParcialResultDto>(StatusCodes.Status200OK)
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
        .Produces(StatusCodes.Status409Conflict)
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
    DateTime? FechaHoraDispositivoUtc = null,
    IReadOnlyList<string>? CodigosBultosEntregados = null,
    IReadOnlyList<BultoNoEntregadoRequestDto>? BultosNoEntregados = null
);

public record RegistrarEntregaParcialRequest(
    Guid EnvioId,
    string IdempotencyKey,
    IReadOnlyList<string>? CodigosBultosEntregados,
    IReadOnlyList<BultoNoEntregadoRequestDto>? BultosNoEntregados,
    string NombreReceptor,
    string DocumentoReceptor,
    string? FirmaDigitalBase64 = null,
    string? FotoEvidenciaBase64 = null,
    double? Latitud = null,
    double? Longitud = null,
    DateTime? FechaHoraDispositivoUtc = null
);

public record FinalizarRutaMobileRequest(
    List<string>? CodigosBultosDevueltos = null,
    string? MotivoInternoCodigo = null,
    string? Observaciones = null
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
