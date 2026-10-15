using UltimaMilla.Frontend.Contracts.Enums;

namespace UltimaMilla.Frontend.Contracts.Modelos.Envios;

/// <summary>
/// Representa una orden de envío en las vistas del portal del comercio y backoffice.
/// </summary>
public record EnvioDto
{
    public Guid Id { get; init; }
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string NombreDestinatario { get; init; } = string.Empty;
    public string DireccionDestino { get; init; } = string.Empty;
    public string? TelefonoDestinatario { get; init; }
    public DateTime FechaCreacion { get; init; }
    public EstadoEnvioDto Estado { get; init; } = EstadoEnvioDto.Admitido;
    public string? Observaciones { get; init; }
    public decimal? PesoKg { get; init; }
    public decimal? VolumenM3 { get; init; }

    /// <summary>
    /// Sello de modificación para control de concurrencia optimista al despachar o asignar ruta.
    /// </summary>
    public string SelloModificacion { get; init; } = string.Empty;
}
