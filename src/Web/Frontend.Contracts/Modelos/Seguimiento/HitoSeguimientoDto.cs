using UltimaMilla.Frontend.Contracts.Enums;

namespace UltimaMilla.Frontend.Contracts.Modelos.Seguimiento;

/// <summary>
/// Evento público registrado en la línea de tiempo del envío (UC-33).
/// Excluye explícitamente información personal de terceros o identificadores internos de la base de datos.
/// </summary>
public record HitoSeguimientoDto
{
    public DateTime Fecha { get; init; }
    public EstadoEnvioDto Estado { get; init; }
    public string DescripcionPublica { get; init; } = string.Empty;
}
