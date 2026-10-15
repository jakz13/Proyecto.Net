namespace UltimaMilla.Frontend.Contracts.Modelos.Zonas;

/// <summary>
/// Representa una zona de cobertura geográfica y sus franjas horarias asociadas (UC-02, RF-02).
/// </summary>
public record ZonaCoberturaDto
{
    public Guid Id { get; init; }
    public Guid OperadorId { get; init; }
    public string Nombre { get; init; } = string.Empty;

    /// <summary>
    /// Definición del polígono en formato GeoJSON o pares de coordenadas [lat, lng].
    /// </summary>
    public string PoligonoGeografico { get; init; } = string.Empty;

    /// <summary>
    /// Color hexadecimal distintivo para la representación gráfica en mapas (ej. "#2563eb").
    /// </summary>
    public string ColorHex { get; init; } = "#2563eb";

    public bool Activa { get; init; } = true;
    public DateTime FechaCreacion { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Franjas horarias de entrega habilitadas para esta zona.
    /// </summary>
    public List<FranjaHorariaDto> FranjasHorarias { get; init; } = new();

    /// <summary>
    /// Cantidad total de franjas horarias configuradas.
    /// </summary>
    public int TotalFranjas => FranjasHorarias.Count;
}
