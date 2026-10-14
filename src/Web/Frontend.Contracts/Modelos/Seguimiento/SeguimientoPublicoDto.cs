using UltimaMilla.Frontend.Contracts.Enums;

namespace UltimaMilla.Frontend.Contracts.Modelos.Seguimiento;

/// <summary>
/// Modelo de datos expuesto al público general para el seguimiento anónimo de envíos (UC-33).
/// No contiene nombres de terceros, direcciones completas, ni IDs primarios de base de datos.
/// </summary>
public record SeguimientoPublicoDto
{
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public EstadoEnvioDto EstadoActual { get; init; } = EstadoEnvioDto.Admitido;
    public string? VentanaHorariaEstimada { get; init; }
    public DateTime FechaUltimaActualizacion { get; init; }
    public bool PermiteReprogramacion { get; init; }
    public IReadOnlyList<HitoSeguimientoDto> HistorialEventos { get; init; } = Array.Empty<HitoSeguimientoDto>();
}
