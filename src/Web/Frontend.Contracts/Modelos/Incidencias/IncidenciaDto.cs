namespace UltimaMilla.Frontend.Contracts.Modelos.Incidencias;

/// <summary>
/// Modelo de datos que representa una incidencia operativa registrada durante la jornada (UC-11).
/// </summary>
public record IncidenciaDto
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string NumeroIncidencia { get; init; } = string.Empty;
    public DateTime FechaRegistro { get; init; } = DateTime.UtcNow;
    public DateTime? FechaResolucion { get; init; }

    /// <summary>
    /// Categoría del incidente: "AveriaVehiculo", "AusenciaRepartidor", "ReclamoDestinatario", "CorteTransito", "PaqueteDanado", "DireccionErronea", "Otro".
    /// </summary>
    public string Tipo { get; init; } = "AveriaVehiculo";

    /// <summary>
    /// Nivel de gravedad: "Baja", "Media", "Alta", "Critica".
    /// </summary>
    public string Severidad { get; init; } = "Media";

    /// <summary>
    /// Estado del ciclo de vida: "Abierta", "EnInvestigacion", "Resuelta", "Cerrada".
    /// </summary>
    public string Estado { get; init; } = "Abierta";

    public string Descripcion { get; init; } = string.Empty;

    // Relaciones operativas opcionales (si aplica)
    public Guid? EnvioAfectadoId { get; init; }
    public string? CodigoSeguimientoEnvio { get; init; }
    public string? DestinatarioEnvio { get; init; }

    public Guid? HojaDeRutaId { get; init; }
    public string? NumeroRuta { get; init; }
    public string? RepartidorNombre { get; init; }
    public string? VehiculoMatricula { get; init; }

    // Auditoría y responsables
    public string DespachadorRegistro { get; init; } = string.Empty;
    public string? DespachadorCierre { get; init; }
    public string? ResolucionDetalle { get; init; }

    /// <summary>
    /// Registro cronológico de notas de seguimiento y cambios de estado.
    /// </summary>
    public List<EntradaBitacoraIncidenciaDto> HistorialSeguimiento { get; init; } = new();
}

public record EntradaBitacoraIncidenciaDto
{
    public DateTime FechaHora { get; init; } = DateTime.UtcNow;
    public string Usuario { get; init; } = string.Empty;
    public string Comentario { get; init; } = string.Empty;
    public string? EstadoAnterior { get; init; }
    public string? NuevoEstado { get; init; }
}

public record EnvioDisponibleIncidenciaDto(
    Guid Id,
    string CodigoSeguimiento,
    string Destinatario,
    string Direccion,
    string EstadoEnvio
);

public record RutaDisponibleIncidenciaDto(
    Guid Id,
    string NumeroRuta,
    string RepartidorNombre,
    string VehiculoMatricula,
    string EstadoRuta
);
