using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Usuarios;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de servicio para la gestión de usuarios e identidades (UC-01, RF-31).
/// Consumido por el Backoffice del Operador.
/// </summary>
public interface IUsuariosApi
{
    /// <summary>
    /// Lista los usuarios aplicando filtros de búsqueda, perfil, estado y paginación.
    /// </summary>
    Task<ResultadoPaginadoDto<UsuarioDto>> ListarUsuariosAsync(FiltroUsuariosDto? filtro = null, CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene un usuario por su identificador único.
    /// </summary>
    Task<UsuarioDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default);

    /// <summary>
    /// Registra un nuevo usuario en el sistema.
    /// </summary>
    Task<UsuarioDto> CrearUsuarioAsync(CrearUsuarioDto nuevoUsuario, CancellationToken cancelacion = default);

    /// <summary>
    /// Actualiza los datos generales de un usuario existente.
    /// </summary>
    Task<UsuarioDto> ActualizarUsuarioAsync(Guid id, ActualizarUsuarioDto usuarioActualizado, CancellationToken cancelacion = default);

    /// <summary>
    /// Modifica el estado activo/inactivo (alta/baja lógica) de un usuario.
    /// </summary>
    Task CambiarEstadoUsuarioAsync(Guid id, bool activo, CancellationToken cancelacion = default);

    /// <summary>
    /// Asigna o modifica el perfil/rol de un usuario.
    /// </summary>
    Task AsignarPerfilAsync(Guid id, Guid perfilId, CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene la lista de perfiles/roles disponibles en el sistema para asignación.
    /// </summary>
    Task<IReadOnlyList<PerfilDto>> ListarPerfilesAsync(CancellationToken cancelacion = default);
}
