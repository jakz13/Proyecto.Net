namespace UltimaMilla.Frontend.Contracts.Modelos.Zonas;

/// <summary>
/// Criterios de búsqueda y filtrado de zonas de cobertura.
/// </summary>
public record FiltroZonasDto
{
    public string? TextoBusqueda { get; init; }
    public bool? SoloActivas { get; init; }
}
