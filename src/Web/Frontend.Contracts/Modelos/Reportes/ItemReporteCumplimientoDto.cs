namespace UltimaMilla.Frontend.Contracts.Modelos.Reportes;

/// <summary>
/// Fila de resultados para reportes de cumplimiento de entregas (por zona, repartidor o comercio).
/// </summary>
public record ItemReporteCumplimientoDto
{
    public string Agrupador { get; init; } = string.Empty;
    public string? DetalleAdicional { get; init; }
    public int TotalAsignados { get; init; }
    public int EntregadosEnPlazo { get; init; }
    public int EntregadosConDemora { get; init; }
    public int NoEntregados { get; init; }

    public decimal PorcentajeEfectividad => TotalAsignados > 0 
        ? Math.Round((decimal)EntregadosEnPlazo / TotalAsignados * 100, 1) 
        : 0;

    public decimal PorcentajeEntregaTotal => TotalAsignados > 0 
        ? Math.Round((decimal)(EntregadosEnPlazo + EntregadosConDemora) / TotalAsignados * 100, 1) 
        : 0;
}
