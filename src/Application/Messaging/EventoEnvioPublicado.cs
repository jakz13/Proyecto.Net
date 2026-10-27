using System.Text.Json;

namespace UltimaMilla.Application.Messaging;

/// <summary>
/// Evento de negocio que la API deja en el outbox ante cada cambio de estado de un envío (UC-39) y que el worker consume
/// para notificar al destinatario (UC-35) y avisar al comercio (UC-36). Incluye el operador para que el worker procese
/// siempre con alcance explícito de inquilino y no contenga datos personales del destinatario.
/// </summary>
public record EventoEnvioPublicado(
    Guid OperadorId,
    Guid EnvioId,
    Guid ComercioOperadorId,
    string CodigoSeguimiento,
    string TipoEventoDominio,
    string? EstadoAnterior,
    string EstadoNuevo,
    DateTime OcurridoEnUtc,
    string CorrelationId)
{
    /// <summary>Tipo de evento del outbox con el que se publica y se enruta este mensaje.</summary>
    public const string TipoMensaje = "EnvioCambioEstado";

    /// <summary>Origen con el que el backoffice reencola un mensaje desde la cola de fallidos (UC-42).</summary>
    public const string OrigenColaFallidos = "ColaFallidos";

    public static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public string Serializar() => JsonSerializer.Serialize(this, OpcionesJson);

    /// <summary>Reconstruye el evento; devuelve <c>null</c> si el payload es inválido o no tiene los datos mínimos.</summary>
    public static EventoEnvioPublicado? Deserializar(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            var evento = JsonSerializer.Deserialize<EventoEnvioPublicado>(payload, OpcionesJson);
            return evento is null || evento.EnvioId == Guid.Empty || evento.OperadorId == Guid.Empty || string.IsNullOrWhiteSpace(evento.EstadoNuevo)
                ? null
                : evento;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Lectura tolerante de datos de contexto de cualquier payload del outbox (los hay de distintos tipos de evento).</summary>
public static class PayloadMensaje
{
    public static string? CorrelationIdDe(string? payload) => LeerTexto(payload, "correlationId");

    public static Guid? OperadorIdDe(string? payload) =>
        Guid.TryParse(LeerTexto(payload, "operadorId"), out var id) && id != Guid.Empty ? id : null;

    private static string? LeerTexto(string? payload, string propiedad)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            using var documento = JsonDocument.Parse(payload);
            if (documento.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var campo in documento.RootElement.EnumerateObject())
            {
                if (string.Equals(campo.Name, propiedad, StringComparison.OrdinalIgnoreCase) && campo.Value.ValueKind == JsonValueKind.String)
                    return campo.Value.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
