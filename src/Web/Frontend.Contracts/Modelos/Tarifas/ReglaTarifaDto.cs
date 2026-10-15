namespace UltimaMilla.Frontend.Contracts.Modelos.Tarifas;

/// <summary>
/// Representa una regla de tarifación para envíos dentro de un cuadro tarifario (UC-03, RF-03).
/// </summary>
public record ReglaTarifaDto
{
    public Guid Id { get; init; }
    public Guid CuadroTarifarioId { get; init; }
    public Guid? ZonaCoberturaId { get; init; }
    public string? NombreZona { get; init; }
    public string ModalidadServicio { get; init; } = string.Empty;
    public decimal PesoDesde { get; init; }
    public decimal PesoHasta { get; init; }
    public decimal VolumenDesde { get; init; }
    public decimal VolumenHasta { get; init; }
    public decimal PrecioBase { get; init; }
    public decimal Recargo { get; init; }
    public decimal Bonificacion { get; init; }

    /// <summary>
    /// Precio final calculado: PrecioBase + Recargo - Bonificación.
    /// </summary>
    public decimal PrecioFinal => Math.Max(0, PrecioBase + Recargo - Bonificacion);
}
