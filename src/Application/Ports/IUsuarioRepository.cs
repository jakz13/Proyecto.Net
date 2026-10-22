using UltimaMilla.Domain.Entities.Identidad;

namespace UltimaMilla.Application.Ports;

public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Usuario>> ObtenerPorIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<Perfil?> ObtenerPerfilPorIdAsync(Guid perfilId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Permiso>> ObtenerPermisosDePerfilAsync(Guid perfilId, CancellationToken cancellationToken = default);
    Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken = default);
    Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken = default);
}
