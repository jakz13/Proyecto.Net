namespace UltimaMilla.Frontend.Contracts.Modelos.Rutas;

/// <summary>
/// Representa un envío en depósito disponible para ser asignado a una hoja de ruta.
/// </summary>
public record EnvioPendienteRutaDto
{
    public Guid Id { get; init; }
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string Destinatario { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public string Zona { get; init; } = string.Empty;
    public string FranjaHoraria { get; init; } = string.Empty;
    public string Turno { get; init; } = "Mañana"; // "Mañana", "Tarde", "Todo el día"
    public decimal PesoKg { get; init; }
    public decimal VolumenM3 { get; init; }
    public string? Observaciones { get; init; }
    public string SelloModificacion { get; init; } = string.Empty;
    public bool AsignadoPreviamente { get; init; }
}
