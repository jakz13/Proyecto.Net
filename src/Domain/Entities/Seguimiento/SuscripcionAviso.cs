using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Seguimiento;

public class SuscripcionAviso : Entity, IMustHaveComercioTenant
{
    public Guid ComercioOperadorId { get; private set; }
    public string TipoEvento { get; private set; } = string.Empty;
    public string UrlWebhook { get; private set; } = string.Empty;
    public string ClaveFirma { get; private set; } = string.Empty;
    public bool Activa { get; private set; }

    protected SuscripcionAviso() { }

    public SuscripcionAviso(
        Guid id,
        Guid comercioOperadorId,
        string tipoEvento,
        string urlWebhook,
        string claveFirma) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la suscripción no puede ser vacío.");

        if (comercioOperadorId == Guid.Empty)
            throw new DomainValidationException("El identificador de comercio-operador no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(tipoEvento))
            throw new DomainValidationException("El tipo de evento no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(urlWebhook))
            throw new DomainValidationException("La URL del webhook no puede ser vacía.");

        if (string.IsNullOrWhiteSpace(claveFirma))
            throw new DomainValidationException("La clave de firma no puede ser vacía.");

        ComercioOperadorId = comercioOperadorId;
        TipoEvento = tipoEvento.Trim();
        UrlWebhook = urlWebhook.Trim();
        ClaveFirma = claveFirma.Trim();
        Activa = true;
    }

    public void Activar()
    {
        Activa = true;
    }

    public void Desactivar()
    {
        Activa = false;
    }

    public void ActualizarWebhook(string urlWebhook, string claveFirma)
    {
        if (string.IsNullOrWhiteSpace(urlWebhook))
            throw new DomainValidationException("La URL del webhook no puede ser vacía.");

        if (string.IsNullOrWhiteSpace(claveFirma))
            throw new DomainValidationException("La clave de firma no puede ser vacía.");

        UrlWebhook = urlWebhook.Trim();
        ClaveFirma = claveFirma.Trim();
    }
}
