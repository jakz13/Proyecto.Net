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
        LogoUrl = logoUrl?.Trim();
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

    public void ActualizarLogo(string? logoUrl)
    {
        LogoUrl = logoUrl?.Trim();
    }
}
