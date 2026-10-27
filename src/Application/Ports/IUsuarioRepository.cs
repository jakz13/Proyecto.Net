using UltimaMilla.Domain.Entities.Identidad;

namespace UltimaMilla.Application.Ports;

public interface IUsuarioRepository
{
    Task<List<Usuario>> ObtenerPorIdsAsync(
        IEnumerable<Guid> usuarioIds,
        Guid? operadorId,
        CancellationToken cancellationToken = default);
}