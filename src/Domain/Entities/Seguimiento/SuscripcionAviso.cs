using System.Collections.Concurrent;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Seguimiento;

public class SuscripcionAviso : Entity, IMustHaveComercioTenant
{
    public const string ContratoV1 = "v1";
    public const string ContratoV2 = "v2";

    private static readonly ConcurrentDictionary<Guid, string> VersionContratoPorSuscripcion = new();

    public Guid ComercioOperadorId { get; private set; }
    public string TipoEvento { get; private set; } = string.Empty;
    public string UrlWebhook { get; private set; } = string.Empty;
    public string ClaveFirma { get; private set; } = string.Empty;
    public bool Activa { get; private set; }

    private string _versionContrato = ContratoV1;

    /// <summary>
    /// Versión del contrato de avisos a comercios (RF-36).
    /// 'v1' mantiene el formato original por compatibilidad hacia atrás; 'v2' incorpora el detalle y resumen de eventos a nivel de bulto.
    /// </summary>
    public string VersionContrato
    {
        get
        {
            if (VersionContratoPorSuscripcion.TryGetValue(Id, out var version))
                return version;

            if (TipoEvento.StartsWith("v2:", StringComparison.OrdinalIgnoreCase))
                return ContratoV2;

            return _versionContrato;
        }
        private set => _versionContrato = value;
    }

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
        _versionContrato = versionNormalizada;
        VersionContratoPorSuscripcion[Id] = versionNormalizada;
    }

    public void ConfigurarVersionContrato(string versionContrato)
    {
        var normalizada = NormalizarVersionContrato(versionContrato, TipoEvento);
        _versionContrato = normalizada;
        VersionContratoPorSuscripcion[Id] = normalizada;
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
