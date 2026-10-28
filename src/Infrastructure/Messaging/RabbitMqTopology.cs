using RabbitMQ.Client;

namespace UltimaMilla.Infrastructure.Messaging;

/// <summary>
/// Topología de colas del procesamiento asíncrono (§6.8):
/// exchange de eventos → cola del worker; reintentos con espera creciente mediante colas con TTL que devuelven el mensaje a la
/// cola del worker; y cola de fallidos para los mensajes que agotaron sus reintentos.
/// </summary>
public static class RabbitMqTopology
{
    public const string ExchangeEventos = "ultimamilla.eventos";
    public const string ExchangeFallidos = "ultimamilla.eventos.dlx";
    public const string ColaWorker = "ultimamilla.eventos.worker";
    public const string ColaFallidos = "ultimamilla.eventos.fallidos";

    public const string CabeceraIntento = "x-intento";
    public const string CabeceraAgregadoOrigen = "agregado-origen";
    public const string CabeceraAgregadoId = "agregado-id";

    /// <summary>Reintentos automáticos antes de derivar a fallidos: el mensaje se intenta como máximo <c>MaximoReintentos + 1</c> veces.</summary>
    public const int MaximoReintentos = 3;

    private static readonly (string Cola, int EsperaMs)[] ColasDeEspera =
    {
        ("ultimamilla.eventos.reintento.5s", 5_000),
        ("ultimamilla.eventos.reintento.20s", 20_000),
        ("ultimamilla.eventos.reintento.60s", 60_000)
    };

    /// <summary>Cola de espera para el reintento número <paramref name="reintento"/> (1 = primer reintento).</summary>
    public static string ColaDeEspera(int reintento) => ColasDeEspera[Math.Clamp(reintento, 1, ColasDeEspera.Length) - 1].Cola;

    public static async Task DeclararAsync(IChannel canal, CancellationToken cancellationToken)
    {
        await canal.ExchangeDeclareAsync(ExchangeEventos, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await canal.ExchangeDeclareAsync(ExchangeFallidos, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        await canal.QueueDeclareAsync(ColaFallidos, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await canal.QueueBindAsync(ColaFallidos, ExchangeFallidos, string.Empty, cancellationToken: cancellationToken);

        // Si el worker rechaza un mensaje sin reencolarlo, va a fallidos en vez de perderse
        await canal.QueueDeclareAsync(
            ColaWorker,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = ExchangeFallidos },
            cancellationToken: cancellationToken);
        await canal.QueueBindAsync(ColaWorker, ExchangeEventos, "#", cancellationToken: cancellationToken);

        foreach (var (cola, esperaMs) in ColasDeEspera)
        {
            await canal.QueueDeclareAsync(
                cola,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = esperaMs,
                    ["x-dead-letter-exchange"] = string.Empty,
                    ["x-dead-letter-routing-key"] = ColaWorker
                },
                cancellationToken: cancellationToken);
        }
    }
}
