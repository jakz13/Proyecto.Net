using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Operador;

public class IdentidadVisual : Entity, IMustHaveTenant
{
    public Guid OperadorId { get; private set; }
    public string? LogoUrl { get; private set; }
    public string ColorPrimario { get; private set; } = string.Empty;
    public string ColorSecundario { get; private set; } = string.Empty;
    public string EmailContacto { get; private set; } = string.Empty;
    public string TelefonoContacto { get; private set; } = string.Empty;
    public string? SitioWeb { get; private set; }
    public string? MensajeComunicaciones { get; private set; }

    /// <summary>Dirección del depósito del operador, escrita por el administrador: de ahí salen las rutas y es el punto de partida de la flota sin posición reportada.</summary>
    public string? DireccionDeposito { get; private set; }
    public double? DepositoLatitud { get; private set; }
    public double? DepositoLongitud { get; private set; }

    protected IdentidadVisual() { }

    public IdentidadVisual(
        Guid id,
        Guid operadorId,
        string colorPrimario,
        string colorSecundario,
        string emailContacto,
        string telefonoContacto,
        string? logoUrl = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la identidad visual no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(colorPrimario))
            throw new DomainValidationException("El color primario no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(colorSecundario))
            throw new DomainValidationException("El color secundario no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(emailContacto))
            throw new DomainValidationException("El email de contacto no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(telefonoContacto))
            throw new DomainValidationException("El teléfono de contacto no puede ser vacío.");

        OperadorId = operadorId;
        ColorPrimario = colorPrimario.Trim();
        ColorSecundario = colorSecundario.Trim();
        EmailContacto = emailContacto.Trim().ToLowerInvariant();
        TelefonoContacto = telefonoContacto.Trim();
        LogoUrl = ValidarLogo(logoUrl);
    }

    public void ActualizarColores(string colorPrimario, string colorSecundario)
    {
        if (string.IsNullOrWhiteSpace(colorPrimario))
            throw new DomainValidationException("El color primario no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(colorSecundario))
            throw new DomainValidationException("El color secundario no puede ser vacío.");

        ColorPrimario = colorPrimario.Trim();
        ColorSecundario = colorSecundario.Trim();
    }

    public void ActualizarContacto(string emailContacto, string telefonoContacto)
    {
        if (string.IsNullOrWhiteSpace(emailContacto))
            throw new DomainValidationException("El email de contacto no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(telefonoContacto))
            throw new DomainValidationException("El teléfono de contacto no puede ser vacío.");

        EmailContacto = emailContacto.Trim().ToLowerInvariant();
        TelefonoContacto = telefonoContacto.Trim();
    }

    public void ActualizarComunicaciones(string? sitioWeb, string? mensajeComunicaciones)
    {
        if (!string.IsNullOrWhiteSpace(sitioWeb)
            && !(Uri.TryCreate(sitioWeb.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
            throw new DomainValidationException("El sitio web debe ser una dirección http(s).");

        SitioWeb = string.IsNullOrWhiteSpace(sitioWeb) ? null : sitioWeb.Trim();
        MensajeComunicaciones = string.IsNullOrWhiteSpace(mensajeComunicaciones) ? null : mensajeComunicaciones.Trim();
    }

    /// <summary>Cambia la dirección del depósito y su ubicación (latitud y longitud van juntas o ninguna). Sin dirección se limpia todo.</summary>
    public void ActualizarDeposito(string? direccion, double? latitud, double? longitud)
    {
        var texto = string.IsNullOrWhiteSpace(direccion) ? null : direccion.Trim();
        if (texto is { Length: > 300 })
            throw new DomainValidationException("La dirección del depósito no puede superar los 300 caracteres.");

        if (texto is null)
        {
            DireccionDeposito = null;
            DepositoLatitud = null;
            DepositoLongitud = null;
            return;
        }

        if (latitud.HasValue != longitud.HasValue)
            throw new DomainValidationException("La ubicación del depósito necesita latitud y longitud.");

        if (latitud is < -90 or > 90 || longitud is < -180 or > 180)
            throw new DomainValidationException("La ubicación del depósito está fuera de rango.");

        DireccionDeposito = texto;
        DepositoLatitud = latitud;
        DepositoLongitud = longitud;
    }

    public void ActualizarLogo(string? logoUrl)
    {
        LogoUrl = ValidarLogo(logoUrl);
    }

    /// <summary>
    /// El logo se muestra en todas las pantallas y en los correos: solo se admiten una imagen subida (/uploads/logos/), una imagen
    /// embebida (data:image/) o una URL http(s). Esquemas como javascript: se rechazan.
    /// </summary>
    private static string? ValidarLogo(string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return null;

        var url = logoUrl.Trim();
        if (url.StartsWith("/uploads/logos/", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            return url;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return url;

        throw new DomainValidationException("La URL del logo debe ser una dirección http(s) o una imagen subida.");
    }
}
