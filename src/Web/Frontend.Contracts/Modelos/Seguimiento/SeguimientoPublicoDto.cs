using UltimaMilla.Frontend.Contracts.Enums;

namespace UltimaMilla.Frontend.Contracts.Modelos.Seguimiento;

/// <summary>
/// Modelo de datos expuesto al público general para el seguimiento anónimo de envíos.
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
    public int TotalBultos { get; init; }
    public int BultosEntregados { get; init; }
    public int BultosPendientes { get; init; }
    public string? AvanceEntregaBultos { get; init; }

    /// <summary>"Envio" o "Retiro": el comprador usa el mismo enlace público para seguir ambos.</summary>
    public string Tipo { get; init; } = "Envio";

    /// <summary>Estado del retiro cuando <see cref="Tipo"/> es "Retiro"; en ese caso <see cref="EstadoActual"/> no aplica.</summary>
    public string? EstadoRetiro { get; init; }

    public string? DescripcionEstado { get; init; }

    public bool EsRetiro => string.Equals(Tipo, "Retiro", StringComparison.OrdinalIgnoreCase);

    public string? OperadorNombre { get; init; }
    public string? OperadorColorPrimario { get; init; }
    public string? OperadorColorSecundario { get; init; }
    public string? OperadorLogoUrl { get; init; }
}
