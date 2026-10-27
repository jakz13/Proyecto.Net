using System.Text;
using RabbitMQ.Client;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Infrastructure.Messaging;

/// <summary>
/// Publica en el exchange de eventos esperando la confirmación del broker: si no confirma, lanza excepción
/// y el outbox conserva el mensaje para reintentarlo.
/// </summary>
public sealed class RabbitMqMessagePublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly RabbitMqConnectionProvider _conexion;
    private readonly SemaphoreSlim _compuerta = new(1, 1);
    private IChannel? _canal;

    public RabbitMqMessagePublisher(RabbitMqConnectionProvider conexion)
    {
        _conexion = conexion;
    }

    public async Task PublicarAsync(MensajeSaliente mensaje, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        await _compuerta.WaitAsync(cancellationToken);
        try
        {
            var canal = await ObtenerCanalAsync(cancellationToken);

            var propiedades = new BasicProperties
            {
                Persistent = true,
                MessageId = mensaje.MessageId.ToString(),
                CorrelationId = mensaje.CorrelationId,
                Type = mensaje.TipoEvento,
                ContentType = "application/json",
                Headers = new Dictionary<string, object?>
                {
                    [RabbitMqTopology.CabeceraAgregadoOrigen] = mensaje.AgregadoOrigen,
                    [RabbitMqTopology.CabeceraAgregadoId] = mensaje.AgregadoId.ToString(),
                    [RabbitMqTopology.CabeceraIntento] = 1
                }
            };

            await canal.BasicPublishAsync(
                RabbitMqTopology.ExchangeEventos,
                mensaje.TipoEvento,
                mandatory: false,
                basicProperties: propiedades,
                body: Encoding.UTF8.GetBytes(mensaje.Payload),
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Un canal en mal estado no se reutiliza: la próxima publicación abre uno nuevo
            await DescartarCanalAsync();
            throw;
        }
        finally
        {
            _compuerta.Release();
        }
    }

    private async Task<IChannel> ObtenerCanalAsync(CancellationToken cancellationToken)
    {
        if (_canal is { IsOpen: true })
            return _canal;

        var conexion = await _conexion.ObtenerConexionAsync(cancellationToken);
        var canal = await conexion.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await RabbitMqTopology.DeclararAsync(canal, cancellationToken);
        _canal = canal;
        return canal;
    }

    private async Task DescartarCanalAsync()
    {
        var canal = _canal;
        _canal = null;
        if (canal is null)
            return;

        try
        {
            await canal.DisposeAsync();
        }
        catch
        {
            // El canal ya estaba roto; no hay nada más que liberar
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DescartarCanalAsync();
        _compuerta.Dispose();
    }
}
