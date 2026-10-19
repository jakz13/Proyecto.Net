namespace UltimaMilla.Frontend.Contracts.Modelos.Tablero;

/// <summary>
/// Contiene el resumen de métricas consolidadas del tablero operativo y las alertas vigentes (RF-29).
/// </summary>
public record ResumenTableroDto
{
    public int TotalRutasEnEjecucion { get; init; }
    public int TotalRepartidoresEnCalle { get; init; }
    public int TotalEnviosEnTransito { get; init; }
    public int TotalEnviosEntregadosHoy { get; init; }
    public int TotalEnviosEnRiesgo { get; init; }
    public DateTime UltimaSincronizacion { get; init; } = DateTime.UtcNow;
    public List<VehiculoEnVivoDto> Vehiculos { get; init; } = new();
    public List<EnvioRiesgoDto> AlertasRiesgo { get; init; } = new();
}
