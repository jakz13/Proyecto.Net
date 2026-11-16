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

        var emailValido = ContactoValidacion.ValidarEmail(email, "del comercio");
        var telefonoValido = ContactoValidacion.ValidarTelefono(telefono, "del comercio");
        var direccionValida = string.IsNullOrWhiteSpace(direccion) ? string.Empty : ContactoValidacion.ValidarDireccionTexto(direccion, "La dirección del comercio");
        ValidarTextoLibre(razonSocial, 150, "La razón social");
        ValidarTextoLibre(nombreFantasia, 100, "El nombre de fantasía");

        RazonSocial = razonSocial.Trim();
        Documento = documento.Trim();
        Email = emailValido;
        Telefono = telefonoValido;
        NombreFantasia = nombreFantasia?.Trim() ?? string.Empty;
        Direccion = direccionValida;
    }

    /// <summary>Largo máximo y sin marcado ni caracteres de control (un nombre con etiquetas HTML se rechaza en vez de guardarse).</summary>
    private static void ValidarTextoLibre(string? valor, int maximo, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return;
        ValidacionTexto.LargoMaximo(valor, maximo, campo);
        if (valor.Any(c => c is '<' or '>' || char.IsControl(c)))
            throw new DomainValidationException($"{campo} contiene caracteres no permitidos.");
    }

    /// <summary>Actualiza todos los datos editables desde el backoffice y renueva el sello de modificación.</summary>
    public void ActualizarPerfil(string razonSocial, string nombreFantasia, string email, string telefono, string direccion)
    {
        // Se valida todo antes de asignar nada: si un dato falla, el comercio queda exactamente como estaba
        var direccionValida = string.IsNullOrWhiteSpace(direccion) ? string.Empty : ContactoValidacion.ValidarDireccionTexto(direccion, "La dirección del comercio");
        ValidarTextoLibre(nombreFantasia, 100, "El nombre de fantasía");
        ContactoValidacion.ValidarEmail(email, "del comercio");
        ContactoValidacion.ValidarTelefono(telefono, "del comercio");

        ActualizarDatos(razonSocial, Documento);
        ActualizarContacto(email, telefono);
        NombreFantasia = nombreFantasia?.Trim() ?? string.Empty;
        Direccion = direccionValida;
        SelloModificacion = Guid.NewGuid();
    }

    public void ActualizarContacto(string email, string telefono)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainValidationException("El email no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(telefono))
            throw new DomainValidationException("El teléfono no puede ser vacío.");

        var emailValido = ContactoValidacion.ValidarEmail(email, "del comercio");
        var telefonoValido = ContactoValidacion.ValidarTelefono(telefono, "del comercio");
        Email = emailValido;
        Telefono = telefonoValido;
    }

    public void ActualizarDatos(string razonSocial, string documento)
    {
        if (string.IsNullOrWhiteSpace(razonSocial))
            throw new DomainValidationException("La razón social no puede ser vacía.");

        if (string.IsNullOrWhiteSpace(documento))
            throw new DomainValidationException("El documento no puede ser vacío.");

        ValidarTextoLibre(razonSocial, 150, "La razón social");
        RazonSocial = razonSocial.Trim();
        Documento = documento.Trim();
    }
}
