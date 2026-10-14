namespace UltimaMilla.Frontend.Contracts.Modelos.Tarifas;

/// <summary>
/// Representa una versión específica del cuadro tarifario de un operador (UC-03, RF-03).
/// </summary>
public record CuadroTarifarioDto
{
    public Guid Id { get; init; }
    public Guid OperadorId { get; init; }
    public int Version { get; init; }
    public DateTime VigenteDesde { get; init; } = DateTime.UtcNow;
    public DateTime? VigenteHasta { get; init; }
    public string? Comentario { get; init; }

    /// <summary>
    /// Indica si esta versión es la que se encuentra actualmente activa y aplicable a nuevos envíos.
    /// </summary>
    public bool EsVigente => !VigenteHasta.HasValue || VigenteHasta.Value > DateTime.UtcNow;

    /// <summary>
    /// Reglas de precios asociadas a esta versión del tarifario.
    /// </summary>
    public List<ReglaTarifaDto> ReglasTarifa { get; init; } = new();

    /// <summary>
    /// Cantidad total de reglas de precio en esta versión.
    /// </summary>
    public int TotalReglas => ReglasTarifa.Count;
}
