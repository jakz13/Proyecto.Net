using System.Text.Json;

namespace UltimaMilla.Application.Messaging;

/// <summary>
/// Evento de negocio que la API deja en el outbox ante cada cambio de estado de un retiro en domicilio y que el worker
/// consume para avisar al comercio. Se publica en la misma transacción que el cambio (patrón outbox) y viaja con su identificador de
/// correlación. No contiene datos personales del comprador.
/// </summary>
public record EventoRetiroPublicado(
    Guid OperadorId,
    Guid RetiroId,
    Guid ComercioOperadorId,
    string CodigoSeguimiento,
    string TipoEventoDominio,
    string? EstadoAnterior,
    string EstadoNuevo,
    DateTime OcurridoEnUtc,
    string CorrelationId,
    IReadOnlyList<string>? Bultos = null)
{
    /// <summary>Tipo de evento del outbox con el que se publica y se enruta este mensaje.</summary>
    public const string TipoMensaje = "RetiroCambioEstado";

    public string Serializar() => JsonSerializer.Serialize(this, EventoEnvioPublicado.OpcionesJson);

    /// <summary>Reconstruye el evento; devuelve <c>null</c> si el payload es inválido o no tiene los datos mínimos.</summary>
    public static EventoRetiroPublicado? Deserializar(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            var evento = JsonSerializer.Deserialize<EventoRetiroPublicado>(payload, EventoEnvioPublicado.OpcionesJson);
            return evento is null || evento.RetiroId == Guid.Empty || evento.OperadorId == Guid.Empty || string.IsNullOrWhiteSpace(evento.EstadoNuevo)
                ? null
                : evento;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
