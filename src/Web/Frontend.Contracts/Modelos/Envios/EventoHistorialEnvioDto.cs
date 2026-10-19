using UltimaMilla.Frontend.Contracts.Enums;

namespace UltimaMilla.Frontend.Contracts.Modelos.Envios;

/// <summary>
/// Evento de trazabilidad y auditoría histórica del ciclo de vida de un envío (RF-11, RF-12).
/// </summary>
public record EventoHistorialEnvioDto
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid EnvioId { get; init; }
    public DateTime FechaHora { get; init; } = DateTime.UtcNow;
    public EstadoEnvioDto? EstadoAnterior { get; init; }
    public EstadoEnvioDto NuevoEstado { get; init; }
    public string Usuario { get; init; } = "Sistema";
    public string DescripcionEvento { get; init; } = string.Empty;
    public string? MotivoDetalle { get; init; }
    public string? NumeroIncidenciaAsociada { get; init; }
    public bool NotificacionEnviada { get; init; }
}
