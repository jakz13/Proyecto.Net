using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Identidad;

public class Usuario : AggregateRoot
{
    public string Nombre { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public Guid? OperadorId { get; private set; }
    public Guid? ComercioId { get; private set; }
    public Guid PerfilId { get; private set; }
    public bool Activo { get; private set; }
    public DateTime FechaAlta { get; private set; }

    protected Usuario() { }

    public Usuario(
        Guid id,
        string nombre,
        string email,
        string passwordHash,
        Guid perfilId,
        Guid? operadorId = null,
        Guid? comercioId = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del usuario no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainValidationException("El nombre del usuario no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(email))
            throw new DomainValidationException("El email del usuario no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainValidationException("El hash de contraseña no puede ser vacío.");

        if (perfilId == Guid.Empty)
            throw new DomainValidationException("El identificador de perfil no puede ser vacío.");

        Nombre = nombre.Trim();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        PerfilId = perfilId;
        OperadorId = operadorId;
        ComercioId = comercioId;
        Activo = true;
        FechaAlta = DateTime.UtcNow;
    }

    public void Activar()
    {
        Activo = true;
    }

    public void Desactivar()
    {
        Activo = false;
    }

    public void AsignarPerfil(Guid nuevoPerfilId)
    {
        if (nuevoPerfilId == Guid.Empty)
            throw new DomainValidationException("El nuevo perfil no puede ser vacío.");

        PerfilId = nuevoPerfilId;
    }

    public void CambiarPassword(string nuevoPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(nuevoPasswordHash))
            throw new DomainValidationException("El nuevo hash de contraseña no puede ser vacío.");

        PasswordHash = nuevoPasswordHash;
    }

    public void ActualizarDatos(string nombre, string email)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainValidationException("El nombre no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(email))
            throw new DomainValidationException("El email no puede ser vacío.");

        Nombre = nombre.Trim();
        Email = email.Trim().ToLowerInvariant();
    }
}
