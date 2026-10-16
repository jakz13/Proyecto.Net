namespace UltimaMilla.Frontend.Contracts.Modelos.Despacho;

/// <summary>
/// Parámetros enviados por el operario para confirmar la salida del vehículo.
/// </summary>
public record ConfirmarDespachoDto
{
    public Guid HojaDeRutaId { get; init; }
    public List<BultoNoCargadoItemDto> BultosNoCargados { get; init; } = new();
    public bool SimularRutaPreviaSinRendir { get; init; } = false;
    public string? ObservacionesSalida { get; init; }
}

public record BultoNoCargadoItemDto
{
    public Guid EnvioId { get; init; }
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string Motivo { get; init; } = "NoEncontrado"; // "NoEncontrado", "Danado", "DireccionIlegible", "Rechazado"
    public string? Detalle { get; init; }
}
