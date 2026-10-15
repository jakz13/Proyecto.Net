namespace UltimaMilla.Frontend.Contracts.Modelos.Comercios;

/// <summary>
/// Criterios de búsqueda y filtrado para el listado de comercios (UC-01).
/// </summary>
public record FiltroComerciosDto
{
    /// <summary>
    /// Texto libre para buscar por razón social, nombre de fantasía, RUT o correo electrónico.
    /// </summary>
    public string? TextoBusqueda { get; init; }

    /// <summary>
    /// Filtro de estado: null = todos, true = solo activos, false = solo inactivos.
    /// </summary>
    public bool? SoloActivos { get; init; }

    /// <summary>
    /// Número de página solicitada (1-indexed).
    /// </summary>
    public int Pagina { get; init; } = 1;

    /// <summary>
    /// Cantidad de registros por página.
    /// </summary>
    public int TamanoPagina { get; init; } = 10;
}
