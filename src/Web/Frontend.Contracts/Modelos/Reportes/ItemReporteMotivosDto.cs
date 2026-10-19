namespace UltimaMilla.Frontend.Contracts.Modelos.Reportes;

/// <summary>
/// Fila de resultados para el análisis de frecuencia de motivos de no entrega según catálogo.
/// </summary>
public record ItemReporteMotivosDto
{
    public string CodigoMotivo { get; init; } = string.Empty;
    public string DescripcionMotivo { get; init; } = string.Empty;
    public string Categoria { get; init; } = "ExternoDestinatario"; // "Interno", "ExternoDestinatario", "FuerzaMayor"
    public int CantidadOcurrencias { get; init; }
    public decimal PorcentajeDelTotal { get; init; }
    public string ImpactoComun { get; init; } = "Vuelve a Depósito";
}
