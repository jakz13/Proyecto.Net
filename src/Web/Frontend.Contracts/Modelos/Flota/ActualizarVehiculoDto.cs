using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.Flota;

/// <summary>
/// Parámetros para actualizar información y capacidades de un vehículo existente.
/// </summary>
public record ActualizarVehiculoDto
{
    [Required]
    public Guid Id { get; init; }

    [Required(ErrorMessage = "La patente es obligatoria.")]
    [StringLength(15, MinimumLength = 3, ErrorMessage = "La patente debe tener entre 3 y 15 caracteres.")]
    public string Patente { get; init; } = string.Empty;

    [Required(ErrorMessage = "El tipo de vehículo es obligatorio.")]
    [StringLength(50, ErrorMessage = "El tipo no puede superar 50 caracteres.")]
    public string Tipo { get; init; } = "Utilitario";

    [Required(ErrorMessage = "La capacidad en peso es obligatoria.")]
    [Range(0.1, 100000, ErrorMessage = "La capacidad en peso debe ser mayor a 0 kg.")]
    public decimal CapacidadPeso { get; init; }

    [Required(ErrorMessage = "La capacidad en volumen es obligatoria.")]
    [Range(0.01, 1000, ErrorMessage = "La capacidad en volumen debe ser mayor a 0 m³.")]
    public decimal CapacidadVolumen { get; init; }

    public bool Activo { get; init; } = true;
}
