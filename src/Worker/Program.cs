using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;
using UltimaMilla.Application;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Infrastructure;
using UltimaMilla.Infrastructure.Observabilidad;
using UltimaMilla.Worker;
using UltimaMilla.Worker.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

// Puerto propio del Worker para exponer /metrics a Prometheus (por defecto 9464)
var puertoMetricas = builder.Configuration.GetValue<int?>("Metrics:Port") ?? 9464;
builder.WebHost.UseUrls($"http://0.0.0.0:{puertoMetricas}");

// Logs estructurados en JSON compacto con Serilog
builder.Host.UseSerilog((contexto, servicios, configuracion) =>
{
    configuracion
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.With(new EnriquecedorWorkerObservabilidad())
        .WriteTo.Console(new CompactJsonFormatter());
});

// Métricas y trazas distribuidas con OpenTelemetry
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(recurso => recurso.AddService("ultimamilla-worker"))
    .WithMetrics(metricas =>
    {
        metricas
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter(MetricasNegocio.NombreMeter)
            .AddPrometheusExporter();
    })
    .WithTracing(trazas =>
    {
        trazas
            .AddSource(ContextoCorrelacion.FuenteActividades.Name)
            .AddHttpClientInstrumentation();

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            trazas.AddOtlpExporter();
        }
    });

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.Seccion));

// El worker reutiliza los casos de uso y los puertos de la aplicación; no contiene lógica de negocio propia
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<OutboxPublisherBackgroundService>();
builder.Services.AddHostedService<EventConsumerBackgroundService>();
builder.Services.AddHostedService<ScheduledJobsBackgroundService>();

var app = builder.Build();

// Inicializar instrumentos del Meter UltimaMilla para exponer contadores y gauges desde el arranque
_ = app.Services.GetRequiredService<IMetricasNegocio>();

app.MapPrometheusScrapingEndpoint("/metrics").AllowAnonymous();

app.Run();

/// <summary>
/// Enriquecedor de Serilog del Worker que adjunta Service, CorrelationId, TraceId y TenantId a cada evento de log.
/// </summary>
internal sealed class EnriquecedorWorkerObservabilidad : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Service", "ultimamilla-worker"));

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
    }
}
