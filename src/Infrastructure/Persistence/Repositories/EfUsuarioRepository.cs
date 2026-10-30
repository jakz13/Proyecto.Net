using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Identidad;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public class EfUsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _context;

    public EfUsuarioRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var emailNorm = email.Trim().ToLowerInvariant();
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email == emailNorm, cancellationToken);
    }

    public async Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return null;

        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<List<Usuario>> ObtenerPorIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idSet = ids.Where(i => i != Guid.Empty).ToHashSet();
        if (idSet.Count == 0)
            return new List<Usuario>();

        return await _context.Usuarios
            .AsNoTracking()
            .Where(u => idSet.Contains(u.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Usuario>> ObtenerPorIdsAsync(IEnumerable<Guid> usuarioIds, Guid? operadorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usuarioIds);

        if (operadorId is not Guid idOperador || idOperador == Guid.Empty)
            return new List<Usuario>();

        var ids = usuarioIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            return new List<Usuario>();

        return await _context.Usuarios
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.OperadorId == idOperador)
            .ToListAsync(cancellationToken);
    }

    public async Task<Perfil?> ObtenerPerfilPorIdAsync(Guid perfilId, CancellationToken cancellationToken = default)
    {
        if (perfilId == Guid.Empty)
            return null;

        return await _context.Perfiles
            .Include(p => p.Permisos)
            .FirstOrDefaultAsync(p => p.Id == perfilId, cancellationToken);
    }

    public async Task<IReadOnlyList<Permiso>> ObtenerPermisosDePerfilAsync(Guid perfilId, CancellationToken cancellationToken = default)
    {
        if (perfilId == Guid.Empty)
            return Array.Empty<Permiso>();

        var permisosIds = await _context.PerfilPermisos
            .Where(pp => pp.PerfilId == perfilId)
            .Select(pp => pp.PermisoId)
            .ToListAsync(cancellationToken);

        if (permisosIds.Count == 0)
            return Array.Empty<Permiso>();

        return await _context.Permisos
            .Where(p => permisosIds.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        await _context.Usuarios.AddAsync(usuario, cancellationToken);
    }

    public Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        _context.Usuarios.Update(usuario);
        return Task.CompletedTask;
    }

    public async Task<bool> ExisteEmailAsync(string email, Guid? excluirUsuarioId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var emailNormalizado = email.Trim().ToLowerInvariant();
        var consulta = _context.Usuarios.AsNoTracking().Where(u => u.Email == emailNormalizado);
        if (excluirUsuarioId.HasValue && excluirUsuarioId.Value != Guid.Empty)
        {
            consulta = consulta.Where(u => u.Id != excluirUsuarioId.Value);
        }

        return await consulta.AnyAsync(cancellationToken);
    }

    public async Task<Usuario?> ObtenerPorIdYOperadorAsync(Guid id, Guid operadorId, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || operadorId == Guid.Empty)
            return null;

        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Id == id && u.OperadorId == operadorId, cancellationToken);
    }

    public async Task<(IReadOnlyList<Usuario> Elementos, int Total)> ListarUsuariosDelOperadorAsync(
        Guid operadorId,
        string? busqueda,
        Guid? perfilId,
        bool? soloActivos,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default)
    {
        var consulta = _context.Usuarios.AsNoTracking().Where(u => u.OperadorId == operadorId);

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var textoBusqueda = busqueda.Trim();
            var patron = "%" + textoBusqueda.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            consulta = consulta.Where(u => EF.Functions.ILike(u.Nombre, patron, "\\") || EF.Functions.ILike(u.Email, patron, "\\"));
        }

        if (perfilId.HasValue && perfilId.Value != Guid.Empty)
        {
            consulta = consulta.Where(u => u.PerfilId == perfilId.Value);
        }

        if (soloActivos.HasValue)
        {
            consulta = consulta.Where(u => u.Activo == soloActivos.Value);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var elementos = await consulta
            .OrderBy(u => u.Nombre)
            .ThenBy(u => u.Id)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(cancellationToken);

        return (elementos, total);
    }

    public async Task<IReadOnlyList<Perfil>> ListarPerfilesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Perfiles
            .AsNoTracking()
            .OrderBy(p => p.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, string>> ObtenerNombresPerfilesAsync(IEnumerable<Guid> perfilIds, CancellationToken cancellationToken = default)
    {
        var idsUnicos = perfilIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (idsUnicos.Count == 0)
            return new Dictionary<Guid, string>();

        return await _context.Perfiles
            .AsNoTracking()
            .Where(p => idsUnicos.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Nombre, cancellationToken);
    }

    public async Task<Dictionary<Guid, string>> ObtenerNombresComerciosAsync(IEnumerable<Guid> comercioIds, CancellationToken cancellationToken = default)
    {
        var idsUnicos = comercioIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (idsUnicos.Count == 0)
            return new Dictionary<Guid, string>();

        return await _context.Comercios
            .AsNoTracking()
            .Where(c => idsUnicos.Contains(c.Id))
            .ToDictionaryAsync(
                c => c.Id,
                c => string.IsNullOrWhiteSpace(c.NombreFantasia) ? c.RazonSocial : c.NombreFantasia,
                cancellationToken);
    }
}
