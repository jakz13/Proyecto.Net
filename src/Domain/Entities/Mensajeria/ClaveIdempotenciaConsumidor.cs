using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Mensajeria;

public class ClaveIdempotenciaConsumidor : Entity
{
    public string MensajeId { get; private set; } = string.Empty;
    public DateTime ProcesadoEn { get; private set; }
    public string? Resultado { get; private set; }

    protected ClaveIdempotenciaConsumidor() { }

    public ClaveIdempotenciaConsumidor(
        Guid id,
        string mensajeId,
        string? resultado = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(mensajeId))
            throw new DomainValidationException("El identificador del mensaje no puede ser vacío.");

        MensajeId = mensajeId.Trim();
        ProcesadoEn = DateTime.UtcNow;
        Resultado = resultado?.Trim();
    }
}
