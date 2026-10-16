namespace UltimaMilla.Frontend.Contracts.Modelos.Rutas;

/// <summary>
/// Resultado del proceso de reasignación, reprogramación o registro de no entrega de un envío en ruta (UC-10, RF-13, RF-15).
/// </summary>
public record ResultadoReasignacionDto
{
    public bool Exitoso { get; init; }
    public string Mensaje { get; init; } = string.Empty;
    public string NuevoEstadoEnvio { get; init; } = string.Empty;
    public string? NotificacionRepartidor { get; init; }
    public string? RegistroAuditoria { get; init; }
    public List<string> Errores { get; init; } = new();
}
