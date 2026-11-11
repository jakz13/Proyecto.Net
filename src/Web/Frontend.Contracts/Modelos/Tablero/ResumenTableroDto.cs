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

    /// <summary>Retiros en domicilio por asignar a una hoja de ruta (RF 41).</summary>
    public int RetirosPendientes { get; init; }

    /// <summary>Retiros que el operador tiene en gestión: todo lo que no terminó (solicitados, asignados, retirados, no retirados o en depósito) (RF 41).</summary>
    public int RetirosEnCurso { get; init; }
    public DateTime UltimaSincronizacion { get; init; } = DateTime.UtcNow;
    public List<VehiculoEnVivoDto> Vehiculos { get; init; } = new();
    public List<EnvioRiesgoDto> AlertasRiesgo { get; init; } = new();
}
