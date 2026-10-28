using System.Collections.Concurrent;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Indicadores.EjecutarCierreDiario;
using UltimaMilla.Application.Commands.Indicadores.RecalcularIndicadoresCumplimiento;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Tablero;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Api.Endpoints;

/// <summary>
/// Endpoints de soporte para demostrar y verificar los procesos del worker:
/// receptor de prueba de avisos a comercios con verificación de firma HMAC y simulación de fallas (§6.9),
/// y ejecución bajo demanda del recálculo de indicadores (UC-37) y cierre diario (UC-38) del operador autenticado.
/// </summary>
public static class WorkerProcesosEndpoints
{
    private const int MaximoBytesPayload = 64 * 1024;
    private const int MaximoRecibidosEnMemoria = 50;

    private static readonly ConcurrentQueue<WebhookRecibidoDto> Recibidos = new();
    private static volatile int _codigoRespuestaSimulado = StatusCodes.Status200OK;
    private static volatile string _claveFirmaEsperada = "secreto-demo-comercio";

    public static IEndpointRouteBuilder MapWorkerProcesosEndpoints(this IEndpointRouteBuilder app)
    {
        var receptor = app.MapGroup("/api/testing/webhook-receiver")
            .WithTags("Receptor de prueba de Webhooks (§6.9)");

        receptor.MapPost("/", async (HttpRequest request, CancellationToken ct) =>
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
            var clave = _claveFirmaEsperada;
            var firmaValida = !string.IsNullOrWhiteSpace(clave) && FirmaWebhook.EsValida(clave, payload, firma);
            var codigo = _codigoRespuestaSimulado;

            Recibidos.Enqueue(new WebhookRecibidoDto(
                Guid.TryParse(avisoId, out var id) ? id : null,
                DateTime.UtcNow,
                codigo,
                firma,
                firmaValida,
                payload));

            while (Recibidos.Count > MaximoRecibidosEnMemoria && Recibidos.TryDequeue(out _))
            {
            }

            return Results.Json(new { recibido = codigo is >= 200 and < 300, firmaValida }, statusCode: codigo);
        })
        .AllowAnonymous()
        .WithName("RecibirWebhookDePrueba")
        .WithSummary("Receptor de prueba para avisos automáticos a comercios (§6.9)");

        receptor.MapPut("/configuracion", ([FromBody] ConfigurarReceptorWebhookRequest req) =>
        {
            if (req.CodigoRespuestaHttp is < 200 or > 599)
                return Results.BadRequest(new { error = "El código HTTP simulado debe estar entre 200 y 599." });

            _codigoRespuestaSimulado = req.CodigoRespuestaHttp;
            if (!string.IsNullOrWhiteSpace(req.ClaveFirma))
                _claveFirmaEsperada = req.ClaveFirma.Trim();

            return Results.Ok(new { codigoRespuestaHttp = _codigoRespuestaSimulado });
        })
        .RequireAuthorization()
        .WithName("ConfigurarReceptorWebhookDePrueba")
        .WithSummary("Configurar el modo de respuesta (200 OK o falla 5xx) del receptor de prueba");

        receptor.MapGet("/recibidos", () => Results.Ok(Recibidos.Reverse().ToList()))
            .RequireAuthorization()
            .WithName("ListarWebhooksRecibidosDePrueba")
            .WithSummary("Consultar los avisos recibidos por el receptor de prueba y la validez de su firma");

        var procesos = app.MapGroup("/api/operacion/procesos")
            .WithTags("Procesos de operación (UC-37, UC-38)")
            .RequireAuthorization();

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
