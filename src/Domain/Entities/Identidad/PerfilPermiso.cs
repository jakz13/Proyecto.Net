using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Identidad;

public class PerfilPermiso
{
    public Guid PerfilId { get; private set; }
    public Guid PermisoId { get; private set; }

    protected PerfilPermiso() { }

    public PerfilPermiso(Guid perfilId, Guid permisoId)
    {
        if (perfilId == Guid.Empty)
            throw new DomainValidationException("El identificador del perfil no puede ser vacío.");

        if (permisoId == Guid.Empty)
            throw new DomainValidationException("El identificador del permiso no puede ser vacío.");

        PerfilId = perfilId;
        PermisoId = permisoId;
    }
}
