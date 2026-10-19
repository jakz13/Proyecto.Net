namespace UltimaMilla.Frontend.Contracts.Modelos.Reportes;

/// <summary>
/// Contenedor integral de resultados agregados y desglosados del reporte generado (UC-13, RF-30).
/// </summary>
public record ResultadoReporteDto
{
    public FiltroReporteDto Filtro { get; init; } = new();
    public DateTime FechaGeneracion { get; init; } = DateTime.UtcNow;

    public int TotalEnviosProcesados { get; init; }
    public decimal TasaEfectividadGlobal { get; init; }
    public int TotalEntregados { get; init; }
    public int TotalNoEntregados { get; init; }
    public decimal TotalFacturadoLiquidacion { get; init; }

    public List<ItemReporteCumplimientoDto> ItemsCumplimiento { get; init; } = new();
    public List<ItemReporteMotivosDto> ItemsMotivos { get; init; } = new();
    public List<ItemReporteVolumenDto> ItemsVolumen { get; init; } = new();
    public List<ItemReporteLiquidacionDto> ItemsLiquidacion { get; init; } = new();
}
