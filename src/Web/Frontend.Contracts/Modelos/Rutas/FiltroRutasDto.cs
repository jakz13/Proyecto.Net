namespace UltimaMilla.Frontend.Contracts.Modelos.Rutas;

/// <summary>
/// Filtros para la consulta de hojas de ruta planificadas y liberadas.
/// </summary>
public record FiltroRutasDto
{
    public DateOnly? Fecha { get; init; }
    public Guid? RepartidorId { get; init; }
    public string? Estado { get; init; }
    public string? TerminoBusqueda { get; init; }
}
