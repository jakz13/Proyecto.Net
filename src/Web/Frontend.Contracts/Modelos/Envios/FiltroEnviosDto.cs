using UltimaMilla.Frontend.Contracts.Enums;

namespace UltimaMilla.Frontend.Contracts.Modelos.Envios;

/// <summary>
/// Parámetros de filtrado y paginación para el listado de envíos (UC-28).
/// </summary>
public record FiltroEnviosDto
{
    public EstadoEnvioDto? Estado { get; init; }
    public DateTime? FechaDesde { get; init; }
    public DateTime? FechaHasta { get; init; }
    public string? TerminoBusqueda { get; init; }
    public int Pagina { get; init; } = 1;
    public int TamanoPagina { get; init; } = 10;
}
