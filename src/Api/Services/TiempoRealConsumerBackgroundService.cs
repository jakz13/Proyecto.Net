using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UltimaMilla.Application.Ports;
using UltimaMilla.Infrastructure.Messaging;

namespace UltimaMilla.Api.Services;

/// <summary>
/// Lee de la cola los avisos de tiempo real que deja el worker (avisos a comercios, reintentos, cola de fallidos, cierre diario, indicadores) y los difunde por SignalR.
/// Así el worker se comunica solo por la cola de mensajes y los navegadores solo hablan con la API, que es quien tiene los hubs.
/// La cola es compartida: cada aviso lo toma una sola instancia de la API y el respaldo Redis lo reparte a los clientes de las dos.
/// Si RabbitMQ no está configurado o no responde la API sigue funcionando: solo se pierde la actualización automática de lo que hace el worker.
/// </summary>
public sealed class TiempoRealConsumerBackgroundService : BackgroundService
{
    private static readonly TimeSpan EsperaReconexion = TimeSpan.FromSeconds(10);
    private const ushort Prefetch = 20;

    private readonly RabbitMqConnectionProvider _conexion;
    private readonly IOptions<RabbitMqOptions> _opciones;
    private readonly IServiceScopeFactory _alcances;
    private readonly ILogger<TiempoRealConsumerBackgroundService> _logger;

    public TiempoRealConsumerBackgroundService(
        RabbitMqConnectionProvider conexion,
        IOptions<RabbitMqOptions> opciones,
        IServiceScopeFactory alcances,
        ILogger<TiempoRealConsumerBackgroundService> logger)
    {
        _conexion = conexion;
        _opciones = opciones;
        _alcances = alcances;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();   // no demora el arranque de la API

        if (string.IsNullOrWhiteSpace(_opciones.Value.UserName) || string.IsNullOrWhiteSpace(_opciones.Value.Password))
        {
            _logger.LogInformation("RabbitMQ no está configurado en la API: no se difunden los avisos de tiempo real del worker.");
            return;
        }

        var avisoDeCaida = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumirAsync(stoppingToken);
                avisoDeCaida = false;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Una sola advertencia por caída, no una por reintento
                if (!avisoDeCaida)
                    _logger.LogWarning(ex, "Sin conexión con RabbitMQ para los avisos de tiempo real del worker; se reintenta cada {Segundos} s.", EsperaReconexion.TotalSeconds);
                avisoDeCaida = true;
            }

            try
            {
                await Task.Delay(EsperaReconexion, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ConsumirAsync(CancellationToken ct)
    {
        var conexion = await _conexion.ObtenerConexionAsync(ct);
        await using var canal = await conexion.CreateChannelAsync(cancellationToken: ct);

        await RabbitMqTopology.DeclararTiempoRealAsync(canal, ct);
        await canal.BasicQosAsync(0, Prefetch, false, ct);

        var canalCaido = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        canal.ChannelShutdownAsync += (_, _) =>
        {
            canalCaido.TrySetResult();
            return Task.CompletedTask;
        };

        var consumidor = new AsyncEventingBasicConsumer(canal);
        consumidor.ReceivedAsync += async (_, entrega) =>
        {
            await ProcesarAsync(entrega.Body, ct);
            // Mejor esfuerzo: un aviso que no se pudo difundir no se reencola (vencería de todos modos y las pantallas se ponen al día en el siguiente refresco)
            await canal.BasicAckAsync(entrega.DeliveryTag, false, ct);
        };
        await canal.BasicConsumeAsync(RabbitMqTopology.ColaTiempoRealApi, autoAck: false, consumer: consumidor, cancellationToken: ct);

        _logger.LogInformation("Difusión de avisos de tiempo real del worker iniciada sobre la cola {Cola}.", RabbitMqTopology.ColaTiempoRealApi);

        using var registro = ct.Register(() => canalCaido.TrySetResult());
        await canalCaido.Task;
    }

    /// <summary>Difunde un aviso recibido. Público para poder probarlo sin RabbitMQ.</summary>
    public async Task ProcesarAsync(ReadOnlyMemory<byte> cuerpo, CancellationToken ct)
    {
        var aviso = AvisoTiempoReal.Deserializar(cuerpo);
        if (aviso is null)
        {
            _logger.LogDebug("Aviso de tiempo real ilegible descartado.");
            return;
        }

        try
        {
            using var alcance = _alcances.CreateScope();
            var notificador = alcance.ServiceProvider.GetRequiredService<ITableroRealtimeNotifier>();
            await aviso.DifundirAsync(notificador, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo difundir el aviso de tiempo real {Tipo}.", aviso.Tipo);
        }
    }
}
