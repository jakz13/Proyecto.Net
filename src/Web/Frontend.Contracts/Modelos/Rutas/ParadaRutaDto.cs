namespace UltimaMilla.Frontend.Contracts.Modelos.Rutas;

/// <summary>
/// Representa una parada planificada de entrega en una hoja de ruta.
/// </summary>
public record ParadaRutaDto
{
    public Guid Id { get; init; }
    public int Orden { get; init; }
    public Guid EnvioId { get; init; }
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string Destinatario { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public string Zona { get; init; } = string.Empty;
    public string FranjaHoraria { get; init; } = string.Empty;
    public decimal PesoKg { get; init; }
    public decimal VolumenM3 { get; init; }
    public string Estado { get; init; } = "Pendiente";
    public string? MotivoNoEntrega { get; init; }
    public string? ObservacionesIncidencia { get; init; }
    public DateTime? FechaIncidencia { get; init; }
    public string? HistorialReasignacion { get; init; }
}
