using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace UltimaMilla.Infrastructure.Messaging;

/// <summary>
/// Conexión compartida y perezosa a RabbitMQ (con recuperación automática). No se conecta hasta que alguien la usa,
/// de modo que la API, que no publica directamente, no depende del broker.
/// </summary>
public sealed class RabbitMqConnectionProvider : IAsyncDisposable
{
    private readonly RabbitMqOptions _opciones;
    private readonly SemaphoreSlim _compuerta = new(1, 1);
    private IConnection? _conexion;

    public RabbitMqConnectionProvider(IOptions<RabbitMqOptions> opciones)
    {
        _opciones = opciones.Value;
    }

    public async Task<IConnection> ObtenerConexionAsync(CancellationToken cancellationToken)
    {
        if (_conexion is { IsOpen: true })
            return _conexion;

        await _compuerta.WaitAsync(cancellationToken);
        try
        {
            if (_conexion is { IsOpen: true })
                return _conexion;

            if (string.IsNullOrWhiteSpace(_opciones.UserName) || string.IsNullOrWhiteSpace(_opciones.Password))
                throw new InvalidOperationException(
                    "Faltan las credenciales de RabbitMQ: configure RabbitMq:UserName y RabbitMq:Password (user-secrets o variables de entorno).");

            var fabrica = new ConnectionFactory
            {
                HostName = _opciones.HostName,
                Port = _opciones.Port,
                VirtualHost = _opciones.VirtualHost,
                UserName = _opciones.UserName,
                Password = _opciones.Password,
                AutomaticRecoveryEnabled = true,
                ClientProvidedName = "ultimamilla"
            };

            if (_conexion is not null)
                await _conexion.DisposeAsync();

            _conexion = await fabrica.CreateConnectionAsync(cancellationToken);
            return _conexion;
        }
        finally
        {
            _compuerta.Release();
        }
    }

    /// <summary>
    /// Consulta de forma segura la cantidad de mensajes listos en la cola si la conexión con RabbitMQ ya está activa.
    /// Nunca lanza excepción si el broker no está disponible o si el proceso actual (ej. API) no usa RabbitMQ.
    /// </summary>
    public async Task<long?> ObtenerMensajesListosSiConectadoAsync(string nombreCola, CancellationToken cancellationToken = default)
    {
        if (_conexion is not { IsOpen: true })
            return null;

        try
        {
            await using var canal = await _conexion.CreateChannelAsync(cancellationToken: cancellationToken);
            var conteo = await canal.MessageCountAsync(nombreCola, cancellationToken);
            return conteo;
        }
        catch
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_conexion is not null)
            await _conexion.DisposeAsync();

        _compuerta.Dispose();
    }
}
