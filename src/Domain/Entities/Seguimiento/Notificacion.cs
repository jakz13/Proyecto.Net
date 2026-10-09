using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Seguimiento;

public class Notificacion : Entity
{
    public Guid EnvioId { get; init; }
    public string Canal { get; init; } = string.Empty;
    public string TipoEvento { get; init; } = string.Empty;
    public EstadoEnvio? EstadoEnvio { get; init; }
    public DateTime Timestamp { get; init; }

    protected Notificacion() { }

    public Notificacion(
        Guid id,
        Guid envioId,
        string canal,
        string tipoEvento,
        EstadoEnvio? estadoEnvio = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la notificación no puede ser vacío.");

        if (envioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(canal))
            throw new DomainValidationException("El canal de notificación no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(tipoEvento))
            throw new DomainValidationException("El tipo de evento no puede ser vacío.");

        EnvioId = envioId;
        Canal = canal.Trim();
        TipoEvento = tipoEvento.Trim();
        EstadoEnvio = estadoEnvio;
        Timestamp = DateTime.UtcNow;
    }
}
