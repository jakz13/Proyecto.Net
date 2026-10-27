using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Identidad;

namespace UltimaMilla.Application.Tests.Fakes;

public class FakeUsuarioRepository : IUsuarioRepository
{
    public List<Usuario> Usuarios { get; } = new();

    public Task<List<Usuario>> ObtenerPorIdsAsync(
        IEnumerable<Guid> usuarioIds,
        Guid? operadorId,
        CancellationToken cancellationToken = default)
    {
        var idsBuscados = usuarioIds.ToHashSet();

        var usuarios = Usuarios
            .Where(u =>
                idsBuscados.Contains(u.Id) &&
                operadorId.HasValue &&
                u.OperadorId == operadorId.Value)
            .ToList();

        return Task.FromResult(usuarios);
    }
}