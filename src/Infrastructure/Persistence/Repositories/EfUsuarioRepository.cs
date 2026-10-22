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
}
