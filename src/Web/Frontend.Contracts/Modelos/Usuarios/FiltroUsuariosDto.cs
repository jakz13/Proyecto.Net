namespace UltimaMilla.Frontend.Contracts.Modelos.Usuarios;

/// <summary>
/// Criterios de búsqueda y filtrado para el listado de usuarios (UC-01, RF-31).
/// </summary>
public record FiltroUsuariosDto
{
    /// <summary>
    /// Texto libre para buscar por nombre o correo electrónico.
    /// </summary>
    public string? TextoBusqueda { get; init; }

    /// <summary>
    /// Filtrar por identificador de perfil o rol.
    /// </summary>
    public Guid? PerfilId { get; init; }

    /// <summary>
    /// Filtro de estado: null = todos, true = solo activos, false = solo inactivos.
    /// </summary>
    public bool? SoloActivos { get; init; }

    /// <summary>
    /// Número de página (1-indexed).
    /// </summary>
    public int Pagina { get; init; } = 1;

    /// <summary>
    /// Cantidad de registros por página.
    /// </summary>
    public int TamanoPagina { get; init; } = 10;
}
