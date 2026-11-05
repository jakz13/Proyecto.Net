using System.Diagnostics;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using UltimaMilla.Application.Messaging;

namespace UltimaMilla.Api.Observabilidad;

/// <summary>
/// Middleware de correlación (UC-44, §6.12):
/// 1. Lee el encabezado <c>X-Correlation-Id</c> de la solicitud.
/// 2. Si falta, supera los 64 caracteres o contiene caracteres no imprimibles, genera un identificador nuevo (GUID).
/// 3. Lo devuelve siempre en el encabezado <c>X-Correlation-Id</c> de la respuesta.
/// 4. Lo agrega al <see cref="LogContext"/> de Serilog, al alcance de <see cref="ContextoCorrelacion"/> y a <see cref="Activity.Current"/>.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string NombreCabecera = ContextoCorrelacion.NombreCabecera;

    private readonly RequestDelegate _siguiente;

    public CorrelationIdMiddleware(RequestDelegate siguiente)
    {
        ArgumentNullException.ThrowIfNull(siguiente);
        _siguiente = siguiente;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var valorRecibido = contexto.Request.Headers[NombreCabecera].FirstOrDefault();
        var correlationId = ContextoCorrelacion.NormalizarOGenerar(valorRecibido);
        var traceId = Activity.Current?.TraceId.ToString() ?? contexto.TraceIdentifier;
        var tenantId = ExtraerTenantId(contexto);

        contexto.Items["CorrelationId"] = correlationId;
        contexto.Response.Headers[NombreCabecera] = correlationId;

        contexto.Response.OnStarting(() =>
        {
            contexto.Response.Headers[NombreCabecera] = correlationId;
            return Task.CompletedTask;
        });

        Activity.Current?.SetTag(ContextoCorrelacion.EtiquetaActividad, correlationId);
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            Activity.Current?.SetTag("tenant_id", tenantId);
        }

        using var alcanceCorrelacion = ContextoCorrelacion.IniciarAlcance(correlationId, tenantId);
        using var propCorrelacion = LogContext.PushProperty("CorrelationId", correlationId);
        using var propTraza = LogContext.PushProperty("TraceId", traceId);
        using var propTenant = !string.IsNullOrWhiteSpace(tenantId)
            ? LogContext.PushProperty("TenantId", tenantId)
            : null;

        await _siguiente(contexto);
    }

    private static string? ExtraerTenantId(HttpContext contexto)
    {
        var usuario = contexto.User;
        var claimTenant = usuario?.FindFirst("operador_id")?.Value
            ?? usuario?.FindFirst("tenant_id")?.Value
            ?? usuario?.FindFirst("OperadorId")?.Value;

        if (!string.IsNullOrWhiteSpace(claimTenant))
            return claimTenant;

        var cabeceraTenant = contexto.Request.Headers["X-Tenant-Id"].FirstOrDefault()
            ?? contexto.Request.Headers["X-Operador-Id"].FirstOrDefault();

        return string.IsNullOrWhiteSpace(cabeceraTenant) ? null : cabeceraTenant.Trim();
    }
}

/// <summary>
/// Enriquecedor de Serilog que garantiza la presencia de <c>CorrelationId</c>, <c>TraceId</c>, <c>TenantId</c>
/// y el enmascaramiento de campos sensibles/PII (Regla 05).
/// </summary>
public sealed class EnriquecedorObservabilidad : ILogEventEnricher
{
    private static readonly HashSet<string> CamposSensibles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Email", "Correo", "Documento", "DocumentoReceptor", "Dni",
        "Telefono", "TelefonoDestinatario", "Password", "Contrasena",
        "Clave", "ClaveApi", "Token", "FirmaDigitalBase64", "FotoEvidenciaBase64"
    };

    private readonly string _nombreServicio;

    public EnriquecedorObservabilidad(string nombreServicio)
    {
        _nombreServicio = string.IsNullOrWhiteSpace(nombreServicio) ? "ultimamilla" : nombreServicio;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Service", _nombreServicio));

        if (!logEvent.Properties.ContainsKey("CorrelationId"))
        {
            var correlationId = ContextoCorrelacion.CorrelationIdActual
                ?? Activity.Current?.GetTagItem(ContextoCorrelacion.EtiquetaActividad)?.ToString();

            if (!string.IsNullOrWhiteSpace(correlationId))
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", correlationId));
        }

        if (!logEvent.Properties.ContainsKey("TraceId") && Activity.Current is { } actividad)
        {
            var traceId = actividad.TraceId.ToString();
            if (traceId != "00000000000000000000000000000000")
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", traceId));
        }

        if (!logEvent.Properties.ContainsKey("TenantId") && !string.IsNullOrWhiteSpace(ContextoCorrelacion.TenantIdActual))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TenantId", ContextoCorrelacion.TenantIdActual));
        }

        foreach (var clave in logEvent.Properties.Keys.ToList())
        {
            if (CamposSensibles.Contains(clave) && logEvent.Properties[clave] is ScalarValue { Value: string texto } && !string.IsNullOrEmpty(texto))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(clave, EnmascararPii(texto)));
            }
        }
    }

    public static string EnmascararPii(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return "***";

        var limpio = valor.Trim();
        var indiceArroba = limpio.IndexOf('@');
        if (indiceArroba > 1)
            return $"{limpio[0]}***{limpio[indiceArroba..]}";

        if (limpio.Length <= 4)
            return "***";

        return $"{limpio[..2]}***{limpio[^2..]}";
    }
}

