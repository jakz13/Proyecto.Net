using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Identidad;

namespace UltimaMilla.Application.Tests.Fakes;

public class FakeUsuarioRepository : IUsuarioRepository
{
    public List<Usuario> Usuarios { get; } = new();
    public List<Perfil> Perfiles { get; } = new();
    public List<Permiso> Permisos { get; } = new();
    public List<PerfilPermiso> PerfilPermisos { get; } = new();
    public Dictionary<Guid, string> NombresComercios { get; } = new();

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

    public Task<List<Usuario>> ObtenerPorIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idSet = ids.Where(i => i != Guid.Empty).ToHashSet();
        return Task.FromResult(Usuarios.Where(u => idSet.Contains(u.Id)).ToList());
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

    public Task<bool> ExisteEmailAsync(string email, Guid? excluirUsuarioId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Task.FromResult(false);

        var norm = email.Trim().ToLowerInvariant();
        var existe = Usuarios.Any(u => u.Email == norm && (!excluirUsuarioId.HasValue || u.Id != excluirUsuarioId.Value));
        return Task.FromResult(existe);
    }

    public Task<Usuario?> ObtenerPorIdYOperadorAsync(Guid id, Guid operadorId, CancellationToken cancellationToken = default)
    {
        var u = Usuarios.FirstOrDefault(x => x.Id == id && x.OperadorId == operadorId);
        return Task.FromResult(u);
    }

    public Task<(IReadOnlyList<Usuario> Elementos, int Total)> ListarUsuariosDelOperadorAsync(
        Guid operadorId,
        string? busqueda,
        Guid? perfilId,
        bool? soloActivos,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default)
    {
        var query = Usuarios.Where(u => u.OperadorId == operadorId);

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var b = busqueda.Trim().ToLowerInvariant();
            query = query.Where(u => u.Nombre.ToLowerInvariant().Contains(b) || u.Email.ToLowerInvariant().Contains(b));
        }

        if (perfilId.HasValue && perfilId.Value != Guid.Empty)
        {
            query = query.Where(u => u.PerfilId == perfilId.Value);
        }

        if (soloActivos.HasValue)
        {
            query = query.Where(u => u.Activo == soloActivos.Value);
        }

        var lista = query.OrderBy(u => u.Nombre).ThenBy(u => u.Id).ToList();
        var total = lista.Count;
        var paginados = lista.Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina).ToList();
        return Task.FromResult(((IReadOnlyList<Usuario>)paginados, total));
    }

    public Task<IReadOnlyList<Perfil>> ListarPerfilesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Perfil>>(Perfiles.OrderBy(p => p.Nombre).ToList());
    }

    public Task<Dictionary<Guid, string>> ObtenerNombresPerfilesAsync(IEnumerable<Guid> perfilIds, CancellationToken cancellationToken = default)
    {
        var idSet = perfilIds.Where(id => id != Guid.Empty).ToHashSet();
        var dic = Perfiles.Where(p => idSet.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Nombre);
        return Task.FromResult(dic);
    }

    public Task<Dictionary<Guid, string>> ObtenerNombresComerciosAsync(IEnumerable<Guid> comercioIds, CancellationToken cancellationToken = default)
    {
        var idSet = comercioIds.Where(id => id != Guid.Empty).ToHashSet();
        var dic = NombresComercios.Where(kv => idSet.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        return Task.FromResult(dic);
    }
}
