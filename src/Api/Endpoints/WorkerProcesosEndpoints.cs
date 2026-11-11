using System.Text;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Indicadores.EjecutarCierreDiario;
using UltimaMilla.Application.Commands.Indicadores.RecalcularIndicadoresCumplimiento;
using UltimaMilla.Application.Commands.Mensajeria.GestionarWebhooks;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Tablero;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Api.Auth;

namespace UltimaMilla.Api.Endpoints;

/// <summary>
/// Endpoints de soporte para demostrar y verificar los procesos del worker y avisos a comercios:
/// receptor de prueba de avisos a comercios con verificación de firma HMAC y simulación de fallas (RNF 6.9),
/// gestión de suscripciones de webhook por comercio (UC-36), historial y reenvío manual de avisos (UC-30),
/// métricas de aciertos y fallos del caché distribuido por caso de uso (RNF 6.7),
/// y ejecución bajo demanda del recálculo de indicadores (UC-37) y cierre diario (UC-38) del operador autenticado.
/// </summary>
public static class WorkerProcesosEndpoints
{
    private const int MaximoBytesPayload = 64 * 1024;
    private const int MaximoRecibidos = 50;
    private const string ClaveCacheConfiguracion = "testing:webhook-receiver:configuracion";
    private const string ClaveCacheRecibidos = "testing:webhook-receiver:recibidos";
    private static readonly TimeSpan VigenciaEstadoPrueba = TimeSpan.FromHours(6);

    /// <summary>
    /// El receptor de prueba es una herramienta de verificación (escenario de la defensa): recibe sin sesión, porque quien llama es el worker,
    /// así que <b>solo existe si se lo habilita</b> (<c>Avisos:ReceptorDePrueba:Habilitado</c>; por defecto, solo en desarrollo).
    /// Su estado vive en el caché distribuido, no en memoria del proceso: con dos instancias de la API ambas ven lo mismo.
    /// </summary>
    public static bool ReceptorDePruebaHabilitado(IConfiguration configuracion, IHostEnvironment entorno) =>
        configuracion.GetValue<bool?>("Avisos:ReceptorDePrueba:Habilitado") ?? ModoDemo.Permitido(configuracion, entorno);

    private sealed record ConfiguracionReceptor(int CodigoRespuestaHttp, string ClaveFirma);

    private static async Task<ConfiguracionReceptor> LeerConfiguracionAsync(IDistributedCacheService cache, IConfiguration configuracion, CancellationToken ct) =>
        await cache.GetAsync<ConfiguracionReceptor>(ClaveCacheConfiguracion, ct)
        ?? new ConfiguracionReceptor(StatusCodes.Status200OK, configuracion["Avisos:ReceptorDePrueba:Clave"] ?? "secreto-demo-comercio");

    public static IEndpointRouteBuilder MapWorkerProcesosEndpoints(this IEndpointRouteBuilder app)
    {
        var configuracionApp = app.ServiceProvider.GetRequiredService<IConfiguration>();
        if (ReceptorDePruebaHabilitado(configuracionApp, app.ServiceProvider.GetRequiredService<IHostEnvironment>()))
        {
            MapReceptorDePrueba(app);
        }

        return MapSuscripcionesYProcesos(app);
    }

    private static void MapReceptorDePrueba(IEndpointRouteBuilder app)
    {
        var receptor = app.MapGroup("/api/testing/webhook-receiver")
            .WithTags("Receptor de prueba de Webhooks (§6.9)");

        receptor.MapPost("/", async (
            HttpRequest request,
            [FromServices] IDistributedCacheService cache,
            [FromServices] IConfiguration configuracion,
            CancellationToken ct) =>
        {
            if (request.ContentLength is > MaximoBytesPayload)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            using var lector = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
            var buffer = new char[MaximoBytesPayload + 1];
            var leidos = await lector.ReadBlockAsync(buffer, 0, buffer.Length);
            if (leidos > MaximoBytesPayload)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            var payload = new string(buffer, 0, leidos);
            var firma = request.Headers[FirmaWebhook.CabeceraFirma].FirstOrDefault();
            var avisoId = request.Headers[FirmaWebhook.CabeceraIdAviso].FirstOrDefault();
            var estado = await LeerConfiguracionAsync(cache, configuracion, ct);
            var firmaValida = !string.IsNullOrWhiteSpace(estado.ClaveFirma) && FirmaWebhook.EsValida(estado.ClaveFirma, payload, firma);

            var recibidos = await cache.GetAsync<List<WebhookRecibidoDto>>(ClaveCacheRecibidos, ct) ?? new List<WebhookRecibidoDto>();
            recibidos.Add(new WebhookRecibidoDto(
                Guid.TryParse(avisoId, out var id) ? id : null,
                DateTime.UtcNow,
                estado.CodigoRespuestaHttp,
                firma,
                firmaValida,
                payload));
            if (recibidos.Count > MaximoRecibidos)
                recibidos.RemoveRange(0, recibidos.Count - MaximoRecibidos);
            await cache.SetAsync(ClaveCacheRecibidos, recibidos, VigenciaEstadoPrueba, ct);

            return Results.Json(new { recibido = estado.CodigoRespuestaHttp is >= 200 and < 300, firmaValida }, statusCode: estado.CodigoRespuestaHttp);
        })
        .AllowAnonymous()
        .WithName("RecibirWebhookDePrueba")
        .WithSummary("Receptor de prueba para avisos automáticos a comercios (§6.9)");

        receptor.MapPut("/configuracion", async (
            [FromBody] ConfigurarReceptorWebhookRequest req,
            [FromServices] IDistributedCacheService cache,
            [FromServices] IConfiguration configuracion,
            CancellationToken ct) =>
        {
            if (req.CodigoRespuestaHttp is < 200 or > 599)
                return Results.BadRequest(new { error = "El código HTTP simulado debe estar entre 200 y 599." });

            var actual = await LeerConfiguracionAsync(cache, configuracion, ct);
            var clave = string.IsNullOrWhiteSpace(req.ClaveFirma) ? actual.ClaveFirma : req.ClaveFirma.Trim();
            await cache.SetAsync(ClaveCacheConfiguracion, new ConfiguracionReceptor(req.CodigoRespuestaHttp, clave), VigenciaEstadoPrueba, ct);

            return Results.Ok(new { codigoRespuestaHttp = req.CodigoRespuestaHttp });
        })
        .RequireAuthorization(PoliticasApi.Administracion)
        .WithName("ConfigurarReceptorWebhookDePrueba")
        .WithSummary("Configurar el modo de respuesta (200 OK o falla 5xx) del receptor de prueba");

        receptor.MapGet("/recibidos", async ([FromServices] IDistributedCacheService cache, CancellationToken ct) =>
            {
                var recibidos = await cache.GetAsync<List<WebhookRecibidoDto>>(ClaveCacheRecibidos, ct) ?? new List<WebhookRecibidoDto>();
                return Results.Ok(Enumerable.Reverse(recibidos).ToList());
            })
            .RequireAuthorization(PoliticasApi.Administracion)
            .WithName("ListarWebhooksRecibidosDePrueba")
            .WithSummary("Consultar los avisos recibidos por el receptor de prueba y la validez de su firma");
    }

    private static IEndpointRouteBuilder MapSuscripcionesYProcesos(IEndpointRouteBuilder app)
    {
        var suscripciones = app.MapGroup("/api/comercio/webhooks/suscripciones")
            .WithTags("Suscripciones de Webhooks del Comercio (UC-36)")
            .RequireAuthorization(PoliticasApi.Comercio);

        suscripciones.MapPost("/", async (
            [FromBody] CrearSuscripcionAvisoRequest req,
            [FromServices] IMediator mediator,
            [FromServices] ICurrentTenant tenant,
            CancellationToken ct) =>
        {
            try
            {
                // Un usuario de comercio solo puede suscribir su propio vínculo (segundo nivel de aislamiento)
                if (tenant.ComercioOperadorId is { } propio && req.ComercioOperadorId is { } pedido && pedido != propio)
                    return Results.Forbid();

                var comercioOperadorId = tenant.ComercioOperadorId ?? req.ComercioOperadorId ?? Guid.Empty;
                var resultado = await mediator.Send(
                    new CrearSuscripcionAvisoCommand(comercioOperadorId, req.TipoEvento, req.UrlWebhook, req.ClaveFirma),
                    ct);
                return Results.Created($"/api/comercio/webhooks/suscripciones/{resultado.Id}", resultado);
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
        .WithName("CrearSuscripcionWebhookComercio")
        .WithSummary("Registrar una nueva suscripción de webhook para avisos automáticos al comercio (UC-36)");

        suscripciones.MapGet("/", async (
            [FromQuery] Guid? comercioOperadorId,
            [FromServices] IMediator mediator,
            [FromServices] ICurrentTenant tenant,
            CancellationToken ct) =>
        {
            var resultado = await mediator.Send(new ListarSuscripcionesAvisoQuery(tenant.ComercioOperadorId ?? comercioOperadorId), ct);
            return Results.Ok(resultado);
        })
        .WithName("ListarSuscripcionesWebhookComercio")
        .WithSummary("Listar las suscripciones de webhook del comercio u operador actual");

        suscripciones.MapDelete("/{id:guid}", async (
            Guid id,
            [FromQuery] Guid? comercioOperadorId,
            [FromServices] IMediator mediator,
            [FromServices] ICurrentTenant tenant,
            CancellationToken ct) =>
        {
            try
            {
                var resultado = await mediator.Send(new DesactivarSuscripcionAvisoCommand(id, tenant.ComercioOperadorId ?? comercioOperadorId), ct);
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
        .WithName("DesactivarSuscripcionWebhookComercio")
        .WithSummary("Desactivar una suscripción de webhook del comercio");

        var operacionWebhooks = app.MapGroup("/api/operacion/webhooks")
            .WithTags("Historial y Reenvío de Webhooks (UC-30)")
            .RequireAuthorization(PoliticasApi.Operaciones);

        operacionWebhooks.MapGet("/historial", async (
            [FromQuery] Guid? comercioOperadorId,
            [FromQuery] string? estado,
            [FromQuery] int? limite,
            [FromServices] IMediator mediator,
            [FromServices] ICurrentTenant tenant,
            CancellationToken ct) =>
        {
            var resultado = await mediator.Send(new ListarHistorialAvisosQuery(tenant.ComercioOperadorId ?? comercioOperadorId, estado, limite ?? 50), ct);
            return Results.Ok(resultado);
        })
        .WithName("ListarHistorialWebhooksOperacion")
        .WithSummary("Consultar el historial de avisos automáticos a comercios y su estado de entrega");

        operacionWebhooks.MapPost("/{id:guid}/reenviar", async (
            Guid id,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var resultado = await mediator.Send(new ReenviarAvisoManualCommand(id), ct);
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
        .WithName("ReenviarWebhookManualOperacion")
        .WithSummary("Forzar el reenvío manual de un aviso automático a comercio fallido o agotado (UC-30)");

        var procesos = app.MapGroup("/api/operacion/procesos")
            .WithTags("Procesos de operación (UC-37, UC-38)")
            .RequireAuthorization(PoliticasApi.Administracion);

        procesos.MapPost("/recalcular-indicadores", async (
            [FromQuery] DateOnly? fecha,
            [FromServices] IMediator mediator,
            [FromServices] ICurrentTenant tenant,
            [FromServices] IClock clock,
            CancellationToken ct) =>
        {
            try
            {
                var fechaObjetivo = fecha ?? ZonaHorariaOperacion.FechaLocalDe(clock.UtcNow);
                var resultado = await mediator.Send(new RecalcularIndicadoresCumplimientoCommand(tenant.OperadorId, fechaObjetivo), ct);
                return Results.Ok(resultado);
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RecalcularIndicadoresCumplimiento")
        .WithSummary("Recalcular indicadores de cumplimiento del operador para una fecha (UC-37)");

        procesos.MapPost("/cierre-diario", async (
            [FromQuery] DateOnly? fecha,
            [FromServices] IMediator mediator,
            [FromServices] ICurrentTenant tenant,
            [FromServices] IClock clock,
            CancellationToken ct) =>
        {
            try
            {
                var fechaObjetivo = fecha ?? ZonaHorariaOperacion.FechaLocalDe(clock.UtcNow);
                var resultado = await mediator.Send(new EjecutarCierreDiarioCommand(tenant.OperadorId, fechaObjetivo), ct);
                return Results.Ok(resultado);
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("EjecutarCierreDiarioOperacion")
        .WithSummary("Ejecutar el cierre diario consolidado de la operación (UC-38)");

        app.MapGet("/api/operacion/cache/metricas", (IConfiguration configuration) =>
        {
            var redisConfigurado = !string.IsNullOrWhiteSpace(
                configuration.GetConnectionString("Redis") ?? configuration["Redis:Configuration"]);
            var proveedor = redisConfigurado ? "Redis" : "MemoriaDistribuida";
            return Results.Ok(MedidorCache.ObtenerResumen(proveedor));
        })
        .RequireAuthorization(PoliticasApi.Administracion)
        .WithTags("Caché distribuido (RNF 6.7)")
        .WithName("ObtenerMetricasCacheDistribuido")
        .WithSummary("Consultar las métricas de aciertos, fallos e invalidaciones del caché distribuido por caso de uso")
        .Produces<ResumenMetricasCacheDto>(StatusCodes.Status200OK);

        return app;
    }
}

public record ConfigurarReceptorWebhookRequest(int CodigoRespuestaHttp = 200, string? ClaveFirma = null);

public record WebhookRecibidoDto(
    Guid? AvisoId,
    DateTime RecibidoEnUtc,
    int CodigoRespuestaDevuelto,
    string? FirmaRecibida,
    bool FirmaValida,
    string Payload);
