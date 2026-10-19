namespace UltimaMilla.Frontend.Contracts.Modelos.Rutas;

/// <summary>
/// Registro histórico del log de eventos de reasignación, reprogramación o retiro de envíos en ruta (UC-10, RF-12, RF-15).
/// </summary>
public record EventoReasignacionDto
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime FechaHora { get; init; } = DateTime.UtcNow;
    public Guid HojaDeRutaId { get; init; }
    public string HojaDeRutaNumero { get; init; } = string.Empty;
    public Guid EnvioId { get; init; }
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string Destinatario { get; init; } = string.Empty;
    public string Motivo { get; init; } = string.Empty;
    public string? Detalle { get; init; }
    public string AccionRealizada { get; init; } = string.Empty;
    public string DestinoNuevo { get; init; } = string.Empty;
    public string DespachadorUsuario { get; init; } = string.Empty;
    public string NotificacionRepartidor { get; init; } = string.Empty;
}
