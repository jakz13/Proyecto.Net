namespace UltimaMilla.Frontend.Contracts.Modelos.Tablero;

/// <summary>
/// Representa una parada geográfica de una hoja de ruta monitoreada en tiempo real.
/// </summary>
public record ParadaEnVivoDto
{
    public Guid EnvioId { get; init; }
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public int Orden { get; init; }
    public string Destinatario { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public double Latitud { get; init; }
    public double Longitud { get; init; }
    public string FranjaHoraria { get; init; } = string.Empty;
    public string Estado { get; init; } = "Pendiente"; // "Entregado", "EnCurso", "Pendiente", "NoEntregado"
    public bool EnRiesgo { get; init; }
    public string? MotivoRiesgo { get; init; }
}
