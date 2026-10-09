using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Ejecucion;

public class DispositivoPush : Entity
{
    public Guid UsuarioId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public string Plataforma { get; private set; } = string.Empty;
    public bool Activo { get; private set; }
    public DateTime FechaRegistro { get; private set; }

    protected DispositivoPush() { }

    public DispositivoPush(
        Guid id,
        Guid usuarioId,
        string token,
        string plataforma) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del dispositivo no puede ser vacío.");

        if (usuarioId == Guid.Empty)
            throw new DomainValidationException("El identificador del usuario no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(token))
            throw new DomainValidationException("El token del dispositivo no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(plataforma))
            throw new DomainValidationException("La plataforma no puede ser vacía.");

        UsuarioId = usuarioId;
        Token = token.Trim();
        Plataforma = plataforma.Trim();
        Activo = true;
        FechaRegistro = DateTime.UtcNow;
    }

    public void Activar()
    {
        Activo = true;
    }

    public void Desactivar()
    {
        Activo = false;
    }

    public void ActualizarToken(string nuevoToken)
    {
        if (string.IsNullOrWhiteSpace(nuevoToken))
            throw new DomainValidationException("El token no puede ser vacío.");

        Token = nuevoToken.Trim();
    }
}
