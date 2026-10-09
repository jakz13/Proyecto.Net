using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Comercio;

public class Comercio : AggregateRoot
{
    public string RazonSocial { get; private set; } = string.Empty;
    public string Documento { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Telefono { get; private set; } = string.Empty;

    protected Comercio() { }

    public Comercio(
        Guid id,
        string razonSocial,
        string documento,
        string email,
        string telefono) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del comercio no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(razonSocial))
            throw new DomainValidationException("La razón social del comercio no puede ser vacía.");

        if (string.IsNullOrWhiteSpace(documento))
            throw new DomainValidationException("El documento del comercio no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(email))
            throw new DomainValidationException("El email del comercio no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(telefono))
            throw new DomainValidationException("El teléfono del comercio no puede ser vacío.");

        RazonSocial = razonSocial.Trim();
        Documento = documento.Trim();
        Email = email.Trim().ToLowerInvariant();
        Telefono = telefono.Trim();
    }

    public void ActualizarContacto(string email, string telefono)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainValidationException("El email no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(telefono))
            throw new DomainValidationException("El teléfono no puede ser vacío.");

        Email = email.Trim().ToLowerInvariant();
        Telefono = telefono.Trim();
    }

    public void ActualizarDatos(string razonSocial, string documento)
    {
        if (string.IsNullOrWhiteSpace(razonSocial))
            throw new DomainValidationException("La razón social no puede ser vacía.");

        if (string.IsNullOrWhiteSpace(documento))
            throw new DomainValidationException("El documento no puede ser vacío.");

        RazonSocial = razonSocial.Trim();
        Documento = documento.Trim();
    }
}
