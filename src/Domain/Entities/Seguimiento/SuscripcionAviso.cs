using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Seguimiento;

public class SuscripcionAviso : Entity, IMustHaveComercioTenant
{
    public const string ContratoV1 = "v1";
    public const string ContratoV2 = "v2";

    public Guid ComercioOperadorId { get; private set; }
    public string TipoEvento { get; private set; } = string.Empty;
    public string UrlWebhook { get; private set; } = string.Empty;
    public string ClaveFirma { get; private set; } = string.Empty;
    public bool Activa { get; private set; }

    /// <summary>
    /// Versión del contrato de avisos a comercios (RF 36). Se persiste por suscripción.
    /// 'v1' mantiene el formato original por compatibilidad hacia atrás; 'v2' incorpora el detalle y resumen de eventos a nivel de bulto
    /// y los eventos de retiro en domicilio.
    /// </summary>
    public string VersionContrato { get; private set; } = ContratoV1;

    protected SuscripcionAviso() { }

    public SuscripcionAviso(
        Guid id,
        Guid comercioOperadorId,
        string tipoEvento,
        string urlWebhook,
        string claveFirma,
        string versionContrato = ContratoV1) : base(id)
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

        var versionNormalizada = NormalizarVersionContrato(versionContrato, tipoEvento);

        ComercioOperadorId = comercioOperadorId;
        TipoEvento = tipoEvento.Trim();
        UrlWebhook = urlWebhook.Trim();
        ClaveFirma = claveFirma.Trim();
        Activa = true;
        VersionContrato = versionNormalizada;
    }

    public void ConfigurarVersionContrato(string versionContrato)
    {
        var normalizada = NormalizarVersionContrato(versionContrato, TipoEvento);
        VersionContrato = normalizada;
    }

    private static string NormalizarVersionContrato(string? versionContrato, string? tipoEvento)
    {
        if (!string.IsNullOrWhiteSpace(tipoEvento) && tipoEvento.Trim().StartsWith("v2:", StringComparison.OrdinalIgnoreCase))
            return ContratoV2;

        if (string.IsNullOrWhiteSpace(versionContrato) || string.Equals(versionContrato.Trim(), ContratoV1, StringComparison.OrdinalIgnoreCase))
            return ContratoV1;

        if (string.Equals(versionContrato.Trim(), ContratoV2, StringComparison.OrdinalIgnoreCase))
            return ContratoV2;

        throw new DomainValidationException(
            $"La versión de contrato de aviso '{versionContrato}' no es válida. Versiones admitidas: {ContratoV1}, {ContratoV2}.");
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
