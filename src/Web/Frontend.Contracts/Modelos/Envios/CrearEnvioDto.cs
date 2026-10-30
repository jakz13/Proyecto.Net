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

    public string? DocumentoDestinatario { get; set; }

    [Required(ErrorMessage = "La dirección de entrega es obligatoria.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "La dirección debe contener al menos 5 caracteres.")]
    public string DireccionDestino { get; set; } = string.Empty;

    public string? Calle { get; set; }
    public string? Numero { get; set; }
    public string Ciudad { get; set; } = "Montevideo";

    [Phone(ErrorMessage = "Ingrese un número telefónico válido.")]
    public string? TelefonoDestinatario { get; set; }

    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    public string? EmailDestinatario { get; set; }

    [Required(ErrorMessage = "Seleccione la modalidad de servicio.")]
    public string ModalidadServicio { get; set; } = "Estandar";

    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    public string? Observaciones { get; set; }

    public List<CrearBultoItemDto> Bultos { get; set; } = new()
    {
        new CrearBultoItemDto { Peso = 1.0m, Alto = 10m, Ancho = 10m, Profundo = 10m }
    };

    private decimal? _pesoKg;
    public decimal? PesoKg
    {
        get => _pesoKg ?? (Bultos.Count > 0 ? Bultos.Sum(b => b.Peso) : null);
        set => _pesoKg = value;
    }

    private decimal? _volumenM3;
    public decimal? VolumenM3
    {
        get => _volumenM3 ?? (Bultos.Count > 0 ? Bultos.Sum(b => b.VolumenM3) : null);
        set => _volumenM3 = value;
    }
}

public class CrearBultoItemDto
{
    [Range(0.01, 1000.0, ErrorMessage = "El peso del bulto debe estar entre 0.01 y 1000 kg.")]
    public decimal Peso { get; set; } = 1.0m;

    [Range(0.1, 500.0, ErrorMessage = "El alto debe estar entre 0.1 y 500 cm.")]
    public decimal Alto { get; set; } = 10.0m;

    [Range(0.1, 500.0, ErrorMessage = "El ancho debe estar entre 0.1 y 500 cm.")]
    public decimal Ancho { get; set; } = 10.0m;

    [Range(0.1, 500.0, ErrorMessage = "La profundidad debe estar entre 0.1 y 500 cm.")]
    public decimal Profundo { get; set; } = 10.0m;

    public decimal VolumenM3 => (Alto * Ancho * Profundo) / 1_000_000m;
}
