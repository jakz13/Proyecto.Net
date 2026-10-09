using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Envios;

public class EventoEnvio : Entity
{
    public Guid EnvioId { get; init; }
    public string TipoEvento { get; init; } = string.Empty;
    public EstadoEnvio? EstadoAnterior { get; init; }
    public EstadoEnvio EstadoNuevo { get; init; }
    public DateTime Timestamp { get; init; }
    public string Origen { get; init; } = string.Empty;
    public Guid? ResponsableUsuarioId { get; init; }
    public double? Latitud { get; init; }
    public double? Longitud { get; init; }
    public string? Payload { get; init; }

    protected EventoEnvio() { }

    public EventoEnvio(
        Guid id,
        Guid envioId,
        string tipoEvento,
        EstadoEnvio? estadoAnterior,
        EstadoEnvio estadoNuevo,
        string origen,
        Guid? responsableUsuarioId = null,
        double? latitud = null,
        double? longitud = null,
        string? payload = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del evento no puede ser vacío.");

        if (envioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(tipoEvento))
            throw new DomainValidationException("El tipo de evento no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(origen))
            throw new DomainValidationException("El origen del evento no puede ser vacío.");

        EnvioId = envioId;
        TipoEvento = tipoEvento.Trim();
        EstadoAnterior = estadoAnterior;
        EstadoNuevo = estadoNuevo;
        Timestamp = DateTime.UtcNow;
        Origen = origen.Trim();
        ResponsableUsuarioId = responsableUsuarioId;
        Latitud = latitud;
        Longitud = longitud;
        Payload = payload;
    }
}
