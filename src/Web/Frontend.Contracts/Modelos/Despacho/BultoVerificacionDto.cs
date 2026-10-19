namespace UltimaMilla.Frontend.Contracts.Modelos.Despacho;

/// <summary>
/// Representa un bulto verificado durante el control de salida en depósito (UC-09, UC-18).
/// </summary>
public record BultoVerificacionDto
{
    public Guid BultoId { get; init; }
    public Guid EnvioId { get; init; }
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string CodigoBulto { get; init; } = string.Empty;
    public string Destinatario { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public string Zona { get; init; } = string.Empty;
    public decimal PesoKg { get; init; }
    public decimal VolumenM3 { get; init; }
    public bool EscaneadoEnDeposito { get; init; } = true;
    public bool CargadoEfectivo { get; init; } = true;
    public string? MotivoNoCarga { get; init; }
    public string? Observacion { get; init; }
}
