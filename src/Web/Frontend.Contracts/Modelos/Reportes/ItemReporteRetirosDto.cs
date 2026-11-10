namespace UltimaMilla.Frontend.Contracts.Modelos.Reportes;

/// <summary>
/// Fila del reporte de retiros en domicilio por comercio (RF 41): cuántos se solicitaron en el período, cómo terminaron y cuánto se factura
/// por los que ya llegaron al comercio.
/// </summary>
public record ItemReporteRetirosDto
{
    public Guid ComercioId { get; init; }
    public string Comercio { get; init; } = string.Empty;
    public int Solicitados { get; init; }
    public int Retirados { get; init; }
    public int NoRetirados { get; init; }
    public int EntregadosAlComercio { get; init; }
    public int Cancelados { get; init; }
    public int EnCurso { get; init; }
    public decimal TotalFacturado { get; init; }
}
