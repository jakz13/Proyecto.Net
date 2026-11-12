using System.Diagnostics;
using System.Text;
using MediatR;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;
using UltimaMilla.Application.Commands.Mensajeria.ProcesarMensaje;
using UltimaMilla.Application.Commands.Mensajeria.RegistrarMensajeFallido;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Infrastructure.Messaging;

namespace UltimaMilla.Worker.BackgroundServices;

/// <summary>
/// Consume los eventos de la cola del worker. La idempotencia la resuelve el caso de uso; acá se maneja la entrega:
/// ante una falla el mensaje vuelve por una cola de espera con retraso creciente (5 s, 20 s, 60 s) y, agotados los reintentos,
/// queda registrado en la cola de fallidos para que el administrador lo consulte y lo reintente.
/// </summary>
public class EventConsumerBackgroundService : BackgroundService
{
    private static readonly TimeSpan EsperaReconexion = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnectionProvider _conexion;
    private readonly WorkerOptions _opciones;
    private readonly ILogger<EventConsumerBackgroundService> _logger;

    public EventConsumerBackgroundService(
        IServiceScopeFactory scopeFactory,
        RabbitMqConnectionProvider conexion,
        IOptions<WorkerOptions> opciones,
        ILogger<EventConsumerBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _conexion = conexion;
        _opciones = opciones.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumirAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "El consumidor de eventos se detuvo; se reintenta la conexión en {Segundos}s.", EsperaReconexion.TotalSeconds);
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

    private async Task ConsumirAsync(CancellationToken cancellationToken)
    {
        var conexion = await _conexion.ObtenerConexionAsync(cancellationToken);
        await using var canal = await conexion.CreateChannelAsync(cancellationToken: cancellationToken);

        await RabbitMqTopology.DeclararAsync(canal, cancellationToken);
        await canal.BasicQosAsync(0, _opciones.ConsumidorPrefetch, false, cancellationToken);

        var canalCaido = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        canal.ChannelShutdownAsync += (_, _) =>
        {
            canalCaido.TrySetResult();
            return Task.CompletedTask;
        };

        var consumidor = new AsyncEventingBasicConsumer(canal);
        consumidor.ReceivedAsync += (_, entrega) => ProcesarEntregaAsync(canal, entrega, cancellationToken);
        await canal.BasicConsumeAsync(RabbitMqTopology.ColaWorker, autoAck: false, consumer: consumidor, cancellationToken: cancellationToken);

        _logger.LogInformation("Consumidor de eventos iniciado sobre la cola {Cola}.", RabbitMqTopology.ColaWorker);

        using var registro = cancellationToken.Register(() => canalCaido.TrySetResult());
        await canalCaido.Task;
    }

    private async Task ProcesarEntregaAsync(IChannel canal, BasicDeliverEventArgs entrega, CancellationToken cancellationToken)
    {
        var propiedades = entrega.BasicProperties;

        if (!Guid.TryParse(propiedades.MessageId, out var messageId))
        {
            // Sin identificador no se puede garantizar idempotencia: se deriva a fallidos sin reencolar
            _logger.LogError("Mensaje sin MessageId válido descartado hacia la cola de fallidos.");
            await canal.BasicNackAsync(entrega.DeliveryTag, false, requeue: false, cancellationToken);
            return;
        }

        var intento = LeerEntero(propiedades, RabbitMqTopology.CabeceraIntento) ?? 1;
        var tipoEvento = propiedades.Type ?? string.Empty;
        var agregadoOrigen = LeerTexto(propiedades, RabbitMqTopology.CabeceraAgregadoOrigen) ?? string.Empty;
        _ = Guid.TryParse(LeerTexto(propiedades, RabbitMqTopology.CabeceraAgregadoId), out var agregadoId);
        var payload = Encoding.UTF8.GetString(entrega.Body.Span);

        // Correlación de extremo a extremo: toma el CorrelationId del mensaje o del payload
        var correlationId = ContextoCorrelacion.NormalizarOGenerar(
            !string.IsNullOrWhiteSpace(propiedades.CorrelationId)
                ? propiedades.CorrelationId
                : PayloadMensaje.CorrelationIdDe(payload));
        var operadorDelEvento = PayloadMensaje.OperadorIdDe(payload);
        var tenantId = operadorDelEvento?.ToString();

        using var actividad = ContextoCorrelacion.FuenteActividades.StartActivity("ProcesarEventoCola", ActivityKind.Consumer);
        actividad?.SetTag(ContextoCorrelacion.EtiquetaActividad, correlationId);
        if (!string.IsNullOrWhiteSpace(tenantId))
            actividad?.SetTag("tenant_id", tenantId);

        using var alcanceCorrelacion = ContextoCorrelacion.IniciarAlcance(correlationId, tenantId);
        using var propCorrelacion = LogContext.PushProperty("CorrelationId", correlationId);
        using var propTraza = LogContext.PushProperty("TraceId", actividad?.TraceId.ToString() ?? Activity.Current?.TraceId.ToString());
        using var propTenant = !string.IsNullOrWhiteSpace(tenantId) ? LogContext.PushProperty("TenantId", tenantId) : null;
        using var propMensaje = LogContext.PushProperty("MessageId", messageId);
        using var propTipo = LogContext.PushProperty("TipoEvento", tipoEvento);

        using var traza = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["MessageId"] = messageId,
            ["CorrelationId"] = correlationId,
            ["TipoEvento"] = tipoEvento,
            ["TenantId"] = tenantId
        });

        try
        {
            using var scope = _scopeFactory.CreateScope();
            if (operadorDelEvento is { } idOperador)
                scope.ServiceProvider.GetRequiredService<ICurrentTenant>().EstablecerOperadorDeProceso(idOperador);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var resultado = await mediator.Send(new ProcesarMensajeCommand(messageId, agregadoOrigen, agregadoId, tipoEvento, payload), cancellationToken);

            _logger.LogInformation(
                "Mensaje {Estado} (intento {Intento}, CorrelationId {CorrelationId}): {Notificaciones} notificación(es), {Avisos} aviso(s) a comercios.",
                resultado.Estado, intento, correlationId, resultado.NotificacionesEnviadas, resultado.AvisosGenerados);

            await canal.BasicAckAsync(entrega.DeliveryTag, false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Apagado del worker: sin confirmar, el broker lo vuelve a entregar
        }
        catch (Exception ex)
        {
            await ManejarFalloAsync(canal, entrega, messageId, tipoEvento, agregadoOrigen, agregadoId, payload, intento, ex, cancellationToken);
        }
    }

    private async Task ManejarFalloAsync(
        IChannel canal,
        BasicDeliverEventArgs entrega,
        Guid messageId,
        string tipoEvento,
        string agregadoOrigen,
        Guid agregadoId,
        string payload,
        int intento,
        Exception ex,
        CancellationToken cancellationToken)
    {
        try
        {
            if (intento <= RabbitMqTopology.MaximoReintentos)
            {
                _logger.LogWarning(ex, "Falló el intento {Intento} del mensaje; se reintenta con espera creciente.", intento);
                await PublicarCopiaAsync(canal, entrega, string.Empty, RabbitMqTopology.ColaDeEspera(intento), intento + 1, cancellationToken);
            }
            else
            {
                _logger.LogError(ex, "Se agotaron los reintentos del mensaje (intento {Intento}); pasa a la cola de fallidos.", intento);

                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(
                    new RegistrarMensajeFallidoCommand(messageId, agregadoOrigen, agregadoId, tipoEvento, payload, DescribirFalla(ex), intento, RabbitMqTopology.ColaWorker),
                    cancellationToken);

                await PublicarCopiaAsync(canal, entrega, RabbitMqTopology.ExchangeFallidos, string.Empty, intento, cancellationToken);
            }

            await canal.BasicAckAsync(entrega.DeliveryTag, false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception falla)
        {
            // No se pudo ni reencolar ni registrar el fallo: se devuelve a la cola para no perder el mensaje
            _logger.LogError(falla, "No se pudo derivar el mensaje fallido; se devuelve a la cola.");
            await canal.BasicNackAsync(entrega.DeliveryTag, false, requeue: true, CancellationToken.None);
        }
    }

    private static async Task PublicarCopiaAsync(
        IChannel canal,
        BasicDeliverEventArgs entrega,
        string exchange,
        string routingKey,
        int intento,
        CancellationToken cancellationToken)
    {
        var original = entrega.BasicProperties;
        var cabeceras = new Dictionary<string, object?>
        {
            [RabbitMqTopology.CabeceraAgregadoOrigen] = LeerTexto(original, RabbitMqTopology.CabeceraAgregadoOrigen),
            [RabbitMqTopology.CabeceraAgregadoId] = LeerTexto(original, RabbitMqTopology.CabeceraAgregadoId),
            [RabbitMqTopology.CabeceraIntento] = intento
        };

        var propiedades = new BasicProperties
        {
            Persistent = true,
            MessageId = original.MessageId,
            CorrelationId = original.CorrelationId,
            Type = original.Type,
            ContentType = original.ContentType,
            Headers = cabeceras
        };

        await canal.BasicPublishAsync(exchange, routingKey, mandatory: false, basicProperties: propiedades, body: entrega.Body, cancellationToken: cancellationToken);
    }

    /// <summary>Resumen de la falla para el administrador: tipo y mensaje, sin la traza interna.</summary>
    private static string DescribirFalla(Exception ex)
    {
        var texto = $"{ex.GetType().Name}: {ex.Message}";
        return texto.Length <= 1000 ? texto : texto[..1000];
    }

    private static string? LeerTexto(IReadOnlyBasicProperties propiedades, string clave)
    {
        if (propiedades.Headers is null || !propiedades.Headers.TryGetValue(clave, out var valor))
            return null;

        return valor switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string texto => texto,
            _ => valor?.ToString()
        };
    }

    private static int? LeerEntero(IReadOnlyBasicProperties propiedades, string clave)
    {
        if (propiedades.Headers is null || !propiedades.Headers.TryGetValue(clave, out var valor))
            return null;

        return valor switch
        {
            int entero => entero,
            long largo => (int)largo,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var parseado) => parseado,
            _ => null
        };
    }
}
