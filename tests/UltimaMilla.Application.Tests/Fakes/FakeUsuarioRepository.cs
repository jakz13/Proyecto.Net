using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Identidad;

namespace UltimaMilla.Application.Tests.Fakes;

public class FakeUsuarioRepository : IUsuarioRepository
{
    public List<Usuario> Usuarios { get; } = new();
    public List<Perfil> Perfiles { get; } = new();
    public List<Permiso> Permisos { get; } = new();
    public List<PerfilPermiso> PerfilPermisos { get; } = new();

    public Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Task.FromResult<Usuario?>(null);

        var norm = email.Trim().ToLowerInvariant();
        var u = Usuarios.FirstOrDefault(x => x.Email == norm);
        return Task.FromResult(u);
    }

    public Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var u = Usuarios.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(u);
    }

    public Task<Perfil?> ObtenerPerfilPorIdAsync(Guid perfilId, CancellationToken cancellationToken = default)
    {
        var p = Perfiles.FirstOrDefault(x => x.Id == perfilId);
        return Task.FromResult(p);
    }

    public Task<IReadOnlyList<Permiso>> ObtenerPermisosDePerfilAsync(Guid perfilId, CancellationToken cancellationToken = default)
    {
        var permisosIds = PerfilPermisos
            .Where(pp => pp.PerfilId == perfilId)
            .Select(pp => pp.PermisoId)
            .ToList();

        var permisos = Permisos.Where(p => permisosIds.Contains(p.Id)).ToList();
        return Task.FromResult<IReadOnlyList<Permiso>>(permisos);
    }

    public Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        Usuarios.Add(usuario);
        return Task.CompletedTask;
    }

    public Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        var index = Usuarios.FindIndex(u => u.Id == usuario.Id);
        if (index != -1)
        {
            Usuarios[index] = usuario;
        }
        return Task.CompletedTask;
    }
}
