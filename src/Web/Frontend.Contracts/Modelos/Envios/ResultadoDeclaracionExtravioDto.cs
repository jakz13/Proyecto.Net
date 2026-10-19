using UltimaMilla.Frontend.Contracts.Enums;

namespace UltimaMilla.Frontend.Contracts.Modelos.Envios;

/// <summary>
/// Resultado del proceso de declaración de extravío de un envío (UC-43).
/// </summary>
public record ResultadoDeclaracionExtravioDto
{
    public bool Exito { get; init; }
    public string Mensaje { get; init; } = string.Empty;
    public Guid EnvioId { get; init; }
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public EstadoEnvioDto NuevoEstado { get; init; } = EstadoEnvioDto.Extraviado;
    public string? NumeroIncidenciaGenerada { get; init; }
    public DateTime FechaEvento { get; init; } = DateTime.UtcNow;
    public bool NotificacionComercioEnviada { get; init; } = true;
}
