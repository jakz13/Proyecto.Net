using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Ejecucion;

public class OperacionOfflinePendiente : Entity
{
    public string TipoOperacion { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime TimestampCaptura { get; private set; }
    public bool Sincronizada { get; private set; }
    public int IntentosSincronizacion { get; private set; }

    protected OperacionOfflinePendiente() { }

    public OperacionOfflinePendiente(
        Guid id,
        string tipoOperacion,
        string payload,
        DateTime? timestampCaptura = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la operación offline no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(tipoOperacion))
            throw new DomainValidationException("El tipo de operación offline no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(payload))
            throw new DomainValidationException("El payload de la operación offline no puede ser vacío.");

        TipoOperacion = tipoOperacion.Trim();
        Payload = payload.Trim();
        TimestampCaptura = timestampCaptura ?? DateTime.UtcNow;
        Sincronizada = false;
        IntentosSincronizacion = 0;
    }

    public void MarcarSincronizada()
    {
        Sincronizada = true;
    }

    public void RegistrarIntentoFallido()
    {
        IntentosSincronizacion++;
    }
}
