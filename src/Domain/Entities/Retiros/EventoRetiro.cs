using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Retiros;

/// <summary>Registro inmutable de cada cambio del retiro, con marca temporal, origen, responsable y posición cuando corresponda.</summary>
public class EventoRetiro : Entity
{
    public Guid RetiroId { get; init; }
    public string TipoEvento { get; init; } = string.Empty;
    public EstadoRetiro? EstadoAnterior { get; init; }
    public EstadoRetiro EstadoNuevo { get; init; }
    public DateTime Timestamp { get; init; }
    public string Origen { get; init; } = string.Empty;
    public Guid? ResponsableUsuarioId { get; init; }
    public double? Latitud { get; init; }
    public double? Longitud { get; init; }
    public string? Payload { get; init; }

    protected EventoRetiro() { }

    public EventoRetiro(
        Guid id,
        Guid retiroId,
        string tipoEvento,
        EstadoRetiro? estadoAnterior,
        EstadoRetiro estadoNuevo,
        string origen,
        Guid? responsableUsuarioId = null,
        double? latitud = null,
        double? longitud = null,
        string? payload = null,
        DateTime? timestamp = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del evento no puede ser vacío.");

        if (retiroId == Guid.Empty)
            throw new DomainValidationException("El identificador del retiro no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(tipoEvento))
            throw new DomainValidationException("El tipo de evento no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(origen))
            throw new DomainValidationException("El origen del evento no puede ser vacío.");

        RetiroId = retiroId;
        TipoEvento = tipoEvento.Trim();
        EstadoAnterior = estadoAnterior;
        EstadoNuevo = estadoNuevo;
        Timestamp = timestamp ?? DateTime.UtcNow;
        Origen = origen.Trim();
        ResponsableUsuarioId = responsableUsuarioId;
        Latitud = latitud;
        Longitud = longitud;
        Payload = payload;
    }
}
