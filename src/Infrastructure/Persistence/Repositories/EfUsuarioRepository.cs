using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Identidad;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public sealed class EfUsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _dbContext;

    public EfUsuarioRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<Usuario>> ObtenerPorIdsAsync(
        IEnumerable<Guid> usuarioIds,
        Guid? operadorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usuarioIds);

        if (operadorId is not Guid idOperador ||
            idOperador == Guid.Empty)
        {
            return Task.FromResult(new List<Usuario>());
        }

        var ids = usuarioIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
            return Task.FromResult(new List<Usuario>());

        return _dbContext.Usuarios
            .AsNoTracking()
            .Where(u =>
                ids.Contains(u.Id) &&
                u.OperadorId == idOperador)
            .ToListAsync(cancellationToken);
    }
}