namespace UltimaMilla.Frontend.Contracts.Modelos.Tablero;

/// <summary>
/// Representa el estado de telemetría y ejecución en vivo de un vehículo y su repartidor asignado.
/// </summary>
public record VehiculoEnVivoDto
{
    public Guid HojaDeRutaId { get; init; }
    public string NumeroRuta { get; init; } = string.Empty;
    public Guid RepartidorId { get; init; }
    public string RepartidorNombre { get; init; } = string.Empty;
    public string RepartidorTelefono { get; init; } = string.Empty;
    public Guid VehiculoId { get; init; }
    public string VehiculoMatricula { get; init; } = string.Empty;
    public string VehiculoModelo { get; init; } = string.Empty;
    public double Latitud { get; init; }
    public double Longitud { get; init; }
    public string DireccionAproximada { get; init; } = string.Empty;
    public string ZonaActual { get; init; } = string.Empty;
    public int NivelBateriaPct { get; init; } = 90;
    public int VelocidadKmh { get; init; }
    public DateTime UltimaActualizacion { get; init; } = DateTime.UtcNow;
    public string EstadoOperativo { get; init; } = "EnMovimiento"; // "EnMovimiento", "EnParada", "DetenidoAlerta", "Finalizado"
    public int ParadasCompletadas { get; init; }
    public int TotalParadas { get; init; }
    public string? ParadaActualCodigo { get; init; }
    public string? ParadaActualDireccion { get; init; }
    public bool TieneAlertasRiesgo { get; init; }
    public int CantidadEnviosEnRiesgo { get; init; }
    public List<EnvioRiesgoDto> EnviosEnRiesgo { get; init; } = new();
    public List<ParadaEnVivoDto> Paradas { get; init; } = new();

    public int PorcentajeProgreso => TotalParadas > 0
        ? Math.Min(100, (int)Math.Round((double)ParadasCompletadas / TotalParadas * 100))
        : 0;
}
