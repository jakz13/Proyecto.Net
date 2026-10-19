namespace UltimaMilla.Frontend.Contracts.Modelos.Tablero;

/// <summary>
/// Representa un envío en riesgo de incumplir la franja o ventana horaria comprometida con el cliente (RF-29).
/// </summary>
public record EnvioRiesgoDto
{
    public Guid EnvioId { get; init; }
    public Guid HojaDeRutaId { get; init; }
    public string NumeroRuta { get; init; } = string.Empty;
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string Destinatario { get; init; } = string.Empty;
    public string DestinatarioTelefono { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public string Zona { get; init; } = string.Empty;
    public string FranjaHoraria { get; init; } = string.Empty;
    public DateTime HoraLimiteVentana { get; init; }
    public DateTime HoraEstimadaLlegada { get; init; }
    public int MinutosRetrasoEstimado { get; init; }
    public string NivelRiesgo { get; init; } = "Alto"; // "Critico", "Alto", "Moderado"
    public string CausaRiesgo { get; init; } = string.Empty;
    public string RepartidorNombre { get; init; } = string.Empty;
    public string RepartidorTelefono { get; init; } = string.Empty;
    public string VehiculoMatricula { get; init; } = string.Empty;
    public double Latitud { get; init; }
    public double Longitud { get; init; }
}
