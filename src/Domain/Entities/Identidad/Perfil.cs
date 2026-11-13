using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Identidad;

public class Perfil : AggregateRoot
{
    public string Nombre { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;

    private readonly List<PerfilPermiso> _permisos = new();
    public IReadOnlyCollection<PerfilPermiso> Permisos => _permisos.AsReadOnly();

    protected Perfil() { }

    /// <summary>
    /// Rol con el que viaja el perfil en el token y con el que la API autoriza. El perfil del comercio se muestra como
    /// "Administrador de Comercio", pero su rol de autorización es "Comercio": renombrar el perfil no cambia lo que puede hacer.
    /// </summary>
    public string RolDeAutorizacion =>
        Nombre.Equals("Administrador de Comercio", StringComparison.OrdinalIgnoreCase)
        || Nombre.Equals("ComercioAdmin", StringComparison.OrdinalIgnoreCase)
            ? "Comercio"
            : Nombre;

    /// <summary>
    /// El perfil de plataforma administra a todos los operadores (activa funcionalidades por operador): lo gestiona la plataforma, nunca
    /// el administrador de un operador, que de otro modo podría darse acceso a los demás inquilinos.
    /// </summary>
    public bool EsDePlataforma => Nombre.Equals("AdministradorPlataforma", StringComparison.OrdinalIgnoreCase);

    public Perfil(Guid id, string nombre, string descripcion) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del perfil no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainValidationException("El nombre del perfil no puede ser vacío.");

        Nombre = nombre.Trim();
        Descripcion = descripcion?.Trim() ?? string.Empty;
    }

    public void AsignarPermiso(Guid permisoId)
    {
        if (permisoId == Guid.Empty)
            throw new DomainValidationException("El identificador del permiso no puede ser vacío.");

        if (!_permisos.Any(p => p.PermisoId == permisoId))
        {
            _permisos.Add(new PerfilPermiso(Id, permisoId));
        }
    }

    public void RemoverPermiso(Guid permisoId)
    {
        var permiso = _permisos.FirstOrDefault(p => p.PermisoId == permisoId);
        if (permiso is not null)
        {
            _permisos.Remove(permiso);
        }
    }

    public void ActualizarInformacion(string nombre, string descripcion)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainValidationException("El nombre del perfil no puede ser vacío.");

        Nombre = nombre.Trim();
        Descripcion = descripcion?.Trim() ?? string.Empty;
    }
}
