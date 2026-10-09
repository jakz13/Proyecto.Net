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

        Nombre = nombre.Trim();
        Documento = documento?.Trim();
        Email = email?.Trim().ToLowerInvariant();
        Telefono = telefono?.Trim();
    }

    public void ActualizarContacto(string? email, string? telefono)
    {
        Email = email?.Trim().ToLowerInvariant();
        Telefono = telefono?.Trim();
    }

    public void ActualizarNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainValidationException("El nombre del destinatario no puede ser vacío.");

        Nombre = nombre.Trim();
    }
}
