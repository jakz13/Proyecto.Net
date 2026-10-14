namespace UltimaMilla.Frontend.Contracts.Modelos.Tarifas;

/// <summary>
/// Parámetros de entrada para simular la cotización de un envío bajo la versión tarifaria vigente (UC-03).
/// </summary>
public record SimularTarifaDto
{
    public decimal PesoKg { get; init; } = 2.5m;
    public decimal VolumenM3 { get; init; } = 0.012m;
    public string ModalidadServicio { get; init; } = "Estándar 24-48h";
    public Guid? ZonaCoberturaId { get; init; }
}

/// <summary>
/// Resultado del cálculo tarifario para un envío según las reglas vigentes.
/// </summary>
public record CotizacionSimuladaDto
{
    public bool Exitoso { get; init; }
    public decimal PrecioFinal { get; init; }
    public decimal PrecioBase { get; init; }
    public decimal Recargo { get; init; }
    public decimal Bonificacion { get; init; }
    public int VersionCuadroAplicada { get; init; }
    public string? ReglaAplicada { get; init; }
    public string? Mensaje { get; init; }
}
