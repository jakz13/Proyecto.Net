namespace UltimaMilla.Frontend.Contracts.Modelos.Zonas;

/// <summary>
/// Criterios de búsqueda y filtrado de zonas de cobertura (UC-02, RF-02).
/// </summary>
public record FiltroZonasDto
{
    public string? TextoBusqueda { get; init; }
    public bool? SoloActivas { get; init; }
}
