using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.Envios;

/// <summary>
/// Credencial de API generada para que el sistema externo del comercio invoque la plataforma.
/// </summary>
public record ClaveApiComercioDto(
    string ClaveApi,
    string Aviso,
    DateTime FechaGeneracion);

/// <summary>
/// Solicitud de alta de envío individual a través del canal de integración por API.
/// </summary>
public record SolicitudAltaEnvioApiDto
{
    [Required(ErrorMessage = "La clave de API (X-Api-Key) es obligatoria para autenticar la solicitud.")]
    public string ClaveApi { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleccione la modalidad de servicio.")]
    public string ModalidadServicio { get; set; } = "Estandar";

    [Required(ErrorMessage = "El nombre del destinatario es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe contener entre 2 y 100 caracteres.")]
    public string NombreDestinatario { get; set; } = string.Empty;

    public string? Documento { get; set; }

    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    public string? Email { get; set; }

    public string? Telefono { get; set; }

    [Required(ErrorMessage = "La calle de destino es obligatoria.")]
    public string Calle { get; set; } = string.Empty;

    [Required(ErrorMessage = "El número de puerta es obligatorio.")]
    public string Numero { get; set; } = string.Empty;

    [Required(ErrorMessage = "La ciudad es obligatoria.")]
    public string Ciudad { get; set; } = "Montevideo";

    public string? Referencia { get; set; }

    public string? ReferenciaExterna { get; set; }

    public List<CrearBultoItemDto> Bultos { get; set; } = new()
    {
        new CrearBultoItemDto { Peso = 1.5m, Alto = 15m, Ancho = 20m, Profundo = 25m }
    };

    public decimal PesoTotalKg => Bultos.Count > 0 ? Bultos.Sum(b => b.Peso) : 0m;

    public decimal VolumenTotalM3 => Bultos.Count > 0 ? Bultos.Sum(b => b.VolumenM3) : 0m;
}

/// <summary>
/// Respuesta emitida por la plataforma tras procesar el alta de un envío por API.
/// </summary>
public record RespuestaAltaEnvioApiDto(
    Guid Id,
    string CodigoSeguimiento,
    string Estado,
    decimal Tarifa,
    IReadOnlyList<string> Bultos,
    bool Duplicado = false);

