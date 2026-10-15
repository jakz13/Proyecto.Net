using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.Envios;

/// <summary>
/// Modelo de datos para el alta individual de envíos desde el Portal del Comercio (UC-24).
/// Regla obligatoria: No incluye comercioId ni operadorId, los cuales son inferidos por la API mediante el JWT.
/// </summary>
public record CrearEnvioDto
{
    [Required(ErrorMessage = "El nombre del destinatario es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe contener al menos 2 caracteres.")]
    public string NombreDestinatario { get; set; } = string.Empty;

    [Required(ErrorMessage = "La dirección de entrega es obligatoria.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "La dirección debe contener al menos 5 caracteres.")]
    public string DireccionDestino { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Ingrese un número telefónico válido.")]
    public string? TelefonoDestinatario { get; set; }

    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    public string? EmailDestinatario { get; set; }

    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    public string? Observaciones { get; set; }

    [Range(0.01, 1000.0, ErrorMessage = "El peso debe estar entre 0.01 y 1000 kg.")]
    public decimal? PesoKg { get; set; }

    [Range(0.001, 100.0, ErrorMessage = "El volumen debe estar entre 0.001 y 100 m³.")]
    public decimal? VolumenM3 { get; set; }
}
