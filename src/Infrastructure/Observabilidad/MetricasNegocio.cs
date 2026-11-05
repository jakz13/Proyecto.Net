using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Ports;
using UltimaMilla.Infrastructure.Messaging;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Observabilidad;

/// <summary>
/// Implementación del puerto <see cref="IMetricasNegocio"/> sobre <see cref="System.Diagnostics.Metrics"/> (UC-44, §6.12).
/// Define el único Meter <c>UltimaMilla</c> con los contadores y gauges observados por Prometheus.
/// Los gauges cachean su lectura unos segundos y nunca propagan excepciones si la base o RabbitMQ no están disponibles.
/// </summary>
public sealed class MetricasNegocio : IMetricasNegocio, IDisposable
{
    public const string NombreMeter = "UltimaMilla";
    private static readonly TimeSpan IntervaloCacheGauges = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly Meter _meter;

    private long _totalAciertosCache;
    private long _totalFallosCache;
    private long _totalIntentosAvisoOk;
    private long _totalIntentosAvisoError;
    private long _totalEventosSincronizacion;

    private long _ultimoColaMensajesPendientes;
    private long _ultimoSyncEventosPendientes;
    private long _ultimoAvisosComercioFallidos;
    private long _ticksUltimaActualizacionGauges;
    private int _actualizandoGauges;

    public long TotalAciertosCache => Interlocked.Read(ref _totalAciertosCache);
    public long TotalFallosCache => Interlocked.Read(ref _totalFallosCache);
    public long TotalIntentosAvisoOk => Interlocked.Read(ref _totalIntentosAvisoOk);
    public long TotalIntentosAvisoError => Interlocked.Read(ref _totalIntentosAvisoError);
    public long TotalEventosSincronizacion => Interlocked.Read(ref _totalEventosSincronizacion);

    public MetricasNegocio(IServiceScopeFactory? scopeFactory = null)
    {
        _scopeFactory = scopeFactory;
        _meter = new Meter(NombreMeter, "1.0.0");

        _meter.CreateObservableCounter<long>(
            "um_cache_aciertos_total",
            () => Interlocked.Read(ref _totalAciertosCache),
            description: "Cantidad total de lecturas de caché distribuido que devolvieron un dato válido.");

        _meter.CreateObservableCounter<long>(
            "um_cache_fallos_total",
            () => Interlocked.Read(ref _totalFallosCache),
            description: "Cantidad total de lecturas de caché distribuido que no encontraron dato (miss).");

        _meter.CreateObservableCounter<long>(
            "um_avisos_comercio_intentos_total",
            () => new Measurement<long>[]
            {
                new(Interlocked.Read(ref _totalIntentosAvisoOk), new KeyValuePair<string, object?>("resultado", "ok")),
                new(Interlocked.Read(ref _totalIntentosAvisoError), new KeyValuePair<string, object?>("resultado", "error"))
            },
            description: "Cantidad total de intentos de entrega de webhooks a comercios según resultado (ok|error).");

        _meter.CreateObservableCounter<long>(
            "um_sync_eventos_recibidos_total",
            () => Interlocked.Read(ref _totalEventosSincronizacion),
            description: "Cantidad total de eventos de sincronización recibidos desde la aplicación móvil.");

        _meter.CreateObservableGauge<long>(
            "um_cola_mensajes_pendientes",
            () =>
            {
                ActualizarGaugesSiCorresponde();
                return Interlocked.Read(ref _ultimoColaMensajesPendientes);
            },
            description: "Profundidad de la cola: mensajes listos en RabbitMQ más mensajes del outbox pendientes de publicar.");

        _meter.CreateObservableGauge<long>(
            "um_sync_eventos_pendientes",
            () =>
            {
                ActualizarGaugesSiCorresponde();
                return Interlocked.Read(ref _ultimoSyncEventosPendientes);
            },
            description: "Eventos originados en la operación que aún están pendientes de procesar en el outbox.");

        _meter.CreateObservableGauge<long>(
            "um_avisos_comercio_fallidos",
            () =>
            {
                ActualizarGaugesSiCorresponde();
                return Interlocked.Read(ref _ultimoAvisosComercioFallidos);
            },
            description: "Cantidad de avisos a comercios en estado Fallido, Agotado o Reintentando.");
    }

    public void RegistrarAciertoCache()
    {
        Interlocked.Increment(ref _totalAciertosCache);
    }

    public void RegistrarFalloCache()
    {
        Interlocked.Increment(ref _totalFallosCache);
    }

    public void RegistrarIntentoAviso(bool exito)
    {
        if (exito)
        {
            Interlocked.Increment(ref _totalIntentosAvisoOk);
        }
        else
        {
            Interlocked.Increment(ref _totalIntentosAvisoError);
        }
    }

    public void RegistrarEventoSincronizacion(int cantidad = 1)
    {
        if (cantidad <= 0)
            return;

        Interlocked.Add(ref _totalEventosSincronizacion, cantidad);
    }

    private void ActualizarGaugesSiCorresponde()
    {
        if (_scopeFactory is null)
            return;

        var ahoraTicks = DateTime.UtcNow.Ticks;
        var ultimoTicks = Interlocked.Read(ref _ticksUltimaActualizacionGauges);
        if (ahoraTicks - ultimoTicks < IntervaloCacheGauges.Ticks)
            return;

        if (Interlocked.CompareExchange(ref _actualizandoGauges, 1, 0) != 0)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<AppDbContext>();
            if (db is not null)
            {
                var outboxPendientes = db.OutboxMessages
                    .AsNoTracking()
                    .LongCount(m => !m.Procesado);

                var syncPendientes = db.OutboxMessages
                    .AsNoTracking()
                    .LongCount(m => !m.Procesado && (m.AgregadoOrigen == "Envio" || m.AgregadoOrigen == "HojaDeRuta"));

                var avisosFallidos = db.AvisosComercio
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .LongCount(a => a.Estado == "Fallido" || a.Estado == "Agotado" || a.Estado == "Reintentando");

                long enColaBroker = 0;
                var rabbit = scope.ServiceProvider.GetService<RabbitMqConnectionProvider>();
                if (rabbit is not null)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    enColaBroker = rabbit.ObtenerMensajesListosSiConectadoAsync(RabbitMqTopology.ColaWorker, cts.Token)
                        .GetAwaiter()
                        .GetResult() ?? 0;
                }

                Interlocked.Exchange(ref _ultimoColaMensajesPendientes, outboxPendientes + enColaBroker);
                Interlocked.Exchange(ref _ultimoSyncEventosPendientes, syncPendientes);
                Interlocked.Exchange(ref _ultimoAvisosComercioFallidos, avisosFallidos);
            }

            Interlocked.Exchange(ref _ticksUltimaActualizacionGauges, ahoraTicks);
        }
        catch
        {
            // Resiliencia (§7): si la base de datos o RabbitMQ no están disponibles, se conserva el último valor conocido
            Interlocked.Exchange(ref _ticksUltimaActualizacionGauges, ahoraTicks);
        }
        finally
        {
            Interlocked.Exchange(ref _actualizandoGauges, 0);
        }
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
