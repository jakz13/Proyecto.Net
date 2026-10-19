namespace UltimaMilla.Frontend.Contracts.Modelos.Reportes;

/// <summary>
/// Fila de resultados para el volumen de carga procesada y despachada por fecha.
/// </summary>
public record ItemReporteVolumenDto
{
    public DateOnly Fecha { get; init; }
    public int CantidadGuias { get; init; }
    public int CantidadBultos { get; init; }
    public decimal PesoTotalKg { get; init; }
    public decimal VolumenTotalM3 { get; init; }
    public int RutasDespachadas { get; init; }
}
