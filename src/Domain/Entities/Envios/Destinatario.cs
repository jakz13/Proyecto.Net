using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Envios;

public class Destinatario : Entity
{
    public string Nombre { get; private set; } = string.Empty;
    public string? Documento { get; private set; }
    public string? Email { get; private set; }
    public string? Telefono { get; private set; }

    protected Destinatario() { }

    public Destinatario(
        Guid id,
        string nombre,
        string? documento = null,
        string? email = null,
        string? telefono = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del destinatario no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainValidationException("El nombre del destinatario no puede ser vacío.");

        ValidacionTexto.LargoMaximo(nombre, 200, "El nombre del destinatario");
        ValidacionTexto.LargoMaximo(documento, 50, "El documento");
        if (!string.IsNullOrWhiteSpace(documento))
            Cedula.Validar(documento, "del destinatario");
        var emailValido = ContactoValidacion.ValidarEmailOpcional(email, "del destinatario");
        var telefonoValido = ContactoValidacion.ValidarTelefonoOpcional(telefono, "del destinatario");
        if (nombre.Any(c => c is '<' or '>' || char.IsControl(c)))
            throw new DomainValidationException("El nombre del destinatario contiene caracteres no permitidos.");

        Nombre = nombre.Trim();
        Documento = documento?.Trim();
        Email = emailValido;
        Telefono = telefonoValido;
    }

    public void ActualizarContacto(string? email, string? telefono)
    {
        var emailValido = ContactoValidacion.ValidarEmailOpcional(email, "del destinatario");
        var telefonoValido = ContactoValidacion.ValidarTelefonoOpcional(telefono, "del destinatario");

        Email = emailValido;
        Telefono = telefonoValido;
    }

    public void ActualizarNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainValidationException("El nombre del destinatario no puede ser vacío.");

        ValidacionTexto.LargoMaximo(nombre, 200, "El nombre del destinatario");
        Nombre = nombre.Trim();
    }
}
