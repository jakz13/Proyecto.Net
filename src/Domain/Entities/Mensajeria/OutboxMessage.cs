using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Mensajeria;

public class OutboxMessage : Entity
{
    public string AgregadoOrigen { get; private set; } = string.Empty;
    public Guid AgregadoId { get; private set; }
    public string TipoEvento { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime CreadoEn { get; private set; }
    public bool Procesado { get; private set; }
    public DateTime? ProcesadoEn { get; private set; }

    protected OutboxMessage() { }

    public OutboxMessage(
        Guid id,
        string agregadoOrigen,
        Guid agregadoId,
        string tipoEvento,
        string payload) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del mensaje no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(agregadoOrigen))
            throw new DomainValidationException("El agregado origen no puede ser vacío.");

        if (agregadoId == Guid.Empty)
            throw new DomainValidationException("El identificador del agregado no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(tipoEvento))
            throw new DomainValidationException("El tipo de evento no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(payload))
            throw new DomainValidationException("El payload del mensaje no puede ser vacío.");

        AgregadoOrigen = agregadoOrigen.Trim();
        AgregadoId = agregadoId;
        TipoEvento = tipoEvento.Trim();
        Payload = payload.Trim();
        CreadoEn = DateTime.UtcNow;
        Procesado = false;
    }

    public void MarcarProcesado(DateTime? fecha = null)
    {
        Procesado = true;
        ProcesadoEn = fecha ?? DateTime.UtcNow;
    }
}
