using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Infrastructure.Messaging;

/// <summary>Un aviso de tiempo real que el worker deja en la cola y que la API difunde por SignalR. Solo lleva identificadores y estados, nunca datos del negocio.</summary>
public sealed record AvisoTiempoReal(
    string Tipo,
    Guid OperadorId = default,
    Guid ComercioOperadorId = default,
    Guid Id = default,
    Guid ParadaId = default,
    string? Codigo = null,
    string? Estado = null,
    string? Tipo2 = null,
    string? Severidad = null,
    string? Area = null)
{
    public const string TipoRuta = "ruta";
    public const string TipoHito = "hito";
    public const string TipoEnvio = "envio";
    public const string TipoRetiro = "retiro";
    public const string TipoIncidencia = "incidencia";
    public const string TipoComercio = "comercio";
    public const string TipoDatos = "datos";

    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public byte[] Serializar() => JsonSerializer.SerializeToUtf8Bytes(this, Opciones);

    public static AvisoTiempoReal? Deserializar(ReadOnlyMemory<byte> cuerpo)
    {
        try
        {
            return JsonSerializer.Deserialize<AvisoTiempoReal>(cuerpo.Span, Opciones);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Lo que hace la API al recibir el aviso: lo difunde con el mismo notificador (y el mismo aislamiento por operador) que usa para sus propios cambios.</summary>
    public Task DifundirAsync(ITableroRealtimeNotifier n, CancellationToken ct) => Tipo switch
    {
        TipoRuta => n.NotificarCambioRutaAsync(OperadorId, Id, Estado ?? string.Empty, ct),
        TipoHito => n.NotificarHitoParadaAsync(OperadorId, Id, ParadaId, Estado ?? string.Empty, ct),
        TipoEnvio => n.NotificarEnvioCambioEstadoAsync(OperadorId, ComercioOperadorId, Id, Codigo ?? string.Empty, Estado ?? string.Empty, ct),
        TipoRetiro => n.NotificarRetiroCambioEstadoAsync(OperadorId, ComercioOperadorId, Id, Codigo ?? string.Empty, Estado ?? string.Empty, ct),
        TipoIncidencia => n.NotificarIncidenciaCambioAsync(OperadorId, Id, Tipo2 ?? string.Empty, Severidad ?? string.Empty, Estado ?? string.Empty, ct),
        TipoComercio => n.NotificarCambioDeComercioAsync(ComercioOperadorId, Area ?? "general", ct),
        TipoDatos => n.NotificarDatosCambiaronAsync(OperadorId, Area ?? "general", ct),
        _ => Task.CompletedTask
    };
}

/// <summary>
/// Notificador del Worker. El worker es otro proceso y se comunica con el resto solo por la cola de mensajes: en lugar de empujar nada por SignalR deja el aviso
/// en la cola de tiempo real y la API (que sí tiene los hubs) lo difunde. Es de mejor esfuerzo: si RabbitMQ no responde el aviso se descarta, porque el cambio
/// ya está guardado y las pantallas lo verán en su próximo refresco.
/// </summary>
public sealed class ColaTiempoRealNotifier : ITableroRealtimeNotifier, IAsyncDisposable
{
    private readonly RabbitMqConnectionProvider _conexion;
    private readonly ILogger<ColaTiempoRealNotifier> _logger;
    private readonly SemaphoreSlim _compuerta = new(1, 1);
    private IChannel? _canal;

    public ColaTiempoRealNotifier(RabbitMqConnectionProvider conexion, ILogger<ColaTiempoRealNotifier> logger)
    {
        _conexion = conexion;
        _logger = logger;
    }

    public Task NotificarCambioRutaAsync(Guid operadorId, Guid hojaDeRutaId, string estado, CancellationToken ct = default)
        => PublicarAsync(new AvisoTiempoReal(AvisoTiempoReal.TipoRuta, OperadorId: operadorId, Id: hojaDeRutaId, Estado: estado), ct);

    public Task NotificarHitoParadaAsync(Guid operadorId, Guid hojaDeRutaId, Guid paradaId, string estadoParada, CancellationToken ct = default)
        => PublicarAsync(new AvisoTiempoReal(AvisoTiempoReal.TipoHito, OperadorId: operadorId, Id: hojaDeRutaId, ParadaId: paradaId, Estado: estadoParada), ct);

    public Task NotificarEnvioCambioEstadoAsync(Guid operadorId, Guid comercioOperadorId, Guid envioId, string codigoSeguimiento, string estado, CancellationToken ct = default)
        => PublicarAsync(new AvisoTiempoReal(AvisoTiempoReal.TipoEnvio, operadorId, comercioOperadorId, envioId, Codigo: codigoSeguimiento, Estado: estado), ct);

    public Task NotificarRetiroCambioEstadoAsync(Guid operadorId, Guid comercioOperadorId, Guid retiroId, string codigoSeguimiento, string estado, CancellationToken ct = default)
        => PublicarAsync(new AvisoTiempoReal(AvisoTiempoReal.TipoRetiro, operadorId, comercioOperadorId, retiroId, Codigo: codigoSeguimiento, Estado: estado), ct);

    public Task NotificarIncidenciaCambioAsync(Guid operadorId, Guid incidenciaId, string tipo, string severidad, string estado, CancellationToken ct = default)
        => PublicarAsync(new AvisoTiempoReal(AvisoTiempoReal.TipoIncidencia, OperadorId: operadorId, Id: incidenciaId, Tipo2: tipo, Severidad: severidad, Estado: estado), ct);

    public Task NotificarCambioDeComercioAsync(Guid comercioOperadorId, string area, CancellationToken ct = default)
        => PublicarAsync(new AvisoTiempoReal(AvisoTiempoReal.TipoComercio, ComercioOperadorId: comercioOperadorId, Area: area), ct);

    public Task NotificarDatosCambiaronAsync(Guid operadorId, string area, CancellationToken ct = default)
        => PublicarAsync(new AvisoTiempoReal(AvisoTiempoReal.TipoDatos, OperadorId: operadorId, Area: area), ct);

    private async Task PublicarAsync(AvisoTiempoReal aviso, CancellationToken ct)
    {
        await _compuerta.WaitAsync(ct);
        try
        {
            var canal = await ObtenerCanalAsync(ct);
            await canal.BasicPublishAsync(
                RabbitMqTopology.ExchangeTiempoReal,
                routingKey: string.Empty,
                mandatory: false,
                basicProperties: new BasicProperties { ContentType = "application/json", Persistent = false },
                body: aviso.Serializar(),
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // Un canal en mal estado no se reutiliza, y el aviso se descarta: el cambio ya está guardado
            await DescartarCanalAsync();
            _logger.LogDebug(ex, "No se pudo dejar el aviso de tiempo real en la cola.");
        }
        finally
        {
            _compuerta.Release();
        }
    }

    private async Task<IChannel> ObtenerCanalAsync(CancellationToken ct)
    {
        if (_canal is { IsOpen: true })
            return _canal;

        var conexion = await _conexion.ObtenerConexionAsync(ct);
        var canal = await conexion.CreateChannelAsync(cancellationToken: ct);
        await RabbitMqTopology.DeclararTiempoRealAsync(canal, ct);
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
            // El canal ya estaba roto
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DescartarCanalAsync();
        _compuerta.Dispose();
    }
}
