namespace UltimaMilla.Frontend.Contracts.Modelos.Reportes;

/// <summary>
/// Parámetros de filtrado para la generación de reportes de gestión operativa y comercial (UC-13, RF-30).
/// </summary>
public record FiltroReporteDto
{
    public string TipoReporte { get; init; } = "CumplimientoZona"; 
    // Opciones: "CumplimientoZona", "CumplimientoRepartidor", "CumplimientoComercio", "MotivosNoEntrega", "VolumenPeriodo", "LiquidacionComercio"

    public DateOnly FechaDesde { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
    public DateOnly FechaHasta { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public string Preajuste { get; init; } = "30Dias"; // "Hoy", "7Dias", "EsteMes", "30Dias", "Personalizado"

    public Guid? ComercioId { get; init; }
    public Guid? RepartidorId { get; init; }
    public string? Zona { get; init; }
}
