namespace UltimaMilla.Frontend.Contracts.Modelos.Reportes;

/// <summary>
/// Fila de resultados para la liquidación tarifaria y facturación por comercio cliente.
/// </summary>
public record ItemReporteLiquidacionDto
{
    public Guid ComercioId { get; init; }
    public string RazonSocial { get; init; } = string.Empty;
    public string Rut { get; init; } = string.Empty;
    public int CantidadEnviosLiquidados { get; init; }
    public decimal TarifaBaseTotal { get; init; }
    public decimal RecargoPesoTotal { get; init; }
    public decimal RecargoVolumenTotal { get; init; }
    public decimal DescuentoVolumenTotal { get; init; }
    public decimal Subtotal => TarifaBaseTotal + RecargoPesoTotal + RecargoVolumenTotal - DescuentoVolumenTotal;
    public decimal Iva => Math.Round(Subtotal * 0.22m, 2);
    public decimal TotalFacturado => Subtotal + Iva;
}
