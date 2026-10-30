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
    Task<bool> ExisteEmailAsync(string email, Guid? excluirUsuarioId = null, CancellationToken cancellationToken = default);
    Task<Usuario?> ObtenerPorIdYOperadorAsync(Guid id, Guid operadorId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Usuario> Elementos, int Total)> ListarUsuariosDelOperadorAsync(
        Guid operadorId,
        string? busqueda,
        Guid? perfilId,
        bool? soloActivos,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Perfil>> ListarPerfilesAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, string>> ObtenerNombresPerfilesAsync(IEnumerable<Guid> perfilIds, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, string>> ObtenerNombresComerciosAsync(IEnumerable<Guid> comercioIds, CancellationToken cancellationToken = default);
}
