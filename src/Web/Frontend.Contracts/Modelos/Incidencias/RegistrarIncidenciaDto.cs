namespace UltimaMilla.Frontend.Contracts.Modelos.Incidencias;

/// <summary>
/// Parámetros para dar de alta una nueva incidencia operativa (UC-11).
/// </summary>
public record RegistrarIncidenciaDto
{
    public string Tipo { get; init; } = "AveriaVehiculo";
    public string Severidad { get; init; } = "Media";
    public string Descripcion { get; init; } = string.Empty;

    public Guid? EnvioAfectadoId { get; init; }
    public string? CodigoSeguimientoEnvio { get; init; }

    public Guid? HojaDeRutaId { get; init; }
    public string? RepartidorNombre { get; init; }
    public string? VehiculoMatricula { get; init; }

    public string? AccionInmediata { get; init; }
}

/// <summary>
/// Parámetros para agregar una nota o actualizar el estado en la bitácora de seguimiento.
/// </summary>
public record AgregarSeguimientoIncidenciaDto
{
    public Guid IncidenciaId { get; init; }
    public string Comentario { get; init; } = string.Empty;
    public string? NuevoEstado { get; init; }
}

/// <summary>
/// Parámetros para dar por resuelta y cerrar formalmente una incidencia.
/// </summary>
public record CerrarIncidenciaDto
{
    public Guid IncidenciaId { get; init; }
    public string ResolucionDetalle { get; init; } = string.Empty;
}

/// <summary>
/// Criterios de filtrado y búsqueda para el listado de incidencias.
/// </summary>
public record FiltroIncidenciasDto
{
    public string Estado { get; init; } = "Todos";
    public string? Tipo { get; init; }
    public string? Severidad { get; init; }
    public string? Busqueda { get; init; }
    public string? Fecha { get; init; }
}
