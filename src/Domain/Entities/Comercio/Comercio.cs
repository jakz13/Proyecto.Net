using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Comercio;

public class Comercio : AggregateRoot
{
    public string RazonSocial { get; private set; } = string.Empty;
    public string Documento { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Telefono { get; private set; } = string.Empty;
    public string NombreFantasia { get; private set; } = string.Empty;
    public string Direccion { get; private set; } = string.Empty;

    /// <summary>Cambia en cada modificación: permite detectar que otra persona editó el comercio (concurrencia optimista).</summary>
    public Guid SelloModificacion { get; private set; } = Guid.NewGuid();

    protected Comercio() { }

    public Comercio(
        Guid id,
        string razonSocial,
        string documento,
        string email,
        string telefono,
        string? nombreFantasia = null,
        string? direccion = null) : base(id)
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
        NombreFantasia = nombreFantasia?.Trim() ?? string.Empty;
        Direccion = direccion?.Trim() ?? string.Empty;
    }

    /// <summary>Actualiza todos los datos editables desde el backoffice y renueva el sello de modificación.</summary>
    public void ActualizarPerfil(string razonSocial, string nombreFantasia, string email, string telefono, string direccion)
    {
        ActualizarDatos(razonSocial, Documento);
        ActualizarContacto(email, telefono);
        NombreFantasia = nombreFantasia?.Trim() ?? string.Empty;
        Direccion = direccion?.Trim() ?? string.Empty;
        SelloModificacion = Guid.NewGuid();
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
