using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.Tarifas;

/// <summary>
/// Modelo para dar de alta una regla dentro de una nueva versión del cuadro tarifario (UC-03, RF-03).
/// </summary>
public record CrearReglaTarifaDto
{
    /// <summary>
    /// Opcional: Identificador de la zona de cobertura a la que aplica la tarifa. Si es nulo, aplica para todas las zonas.
    /// </summary>
    public Guid? ZonaCoberturaId { get; init; }

    [Required(ErrorMessage = "La modalidad de servicio es obligatoria.")]
    public string ModalidadServicio { get; init; } = "Estándar 24-48h";

    [Range(0, 10000, ErrorMessage = "El peso mínimo no puede ser negativo.")]
    public decimal PesoDesde { get; init; } = 0;

    [Range(0.01, 10000, ErrorMessage = "El peso máximo debe ser mayor a cero.")]
    public decimal PesoHasta { get; init; } = 5;

    [Range(0, 1000, ErrorMessage = "El volumen mínimo no puede ser negativo.")]
    public decimal VolumenDesde { get; init; } = 0;

    [Range(0.001, 1000, ErrorMessage = "El volumen máximo debe ser mayor a cero.")]
    public decimal VolumenHasta { get; init; } = 0.05m;

    [Range(0, 1000000, ErrorMessage = "El precio base no puede ser negativo.")]
    public decimal PrecioBase { get; init; } = 180;

    [Range(0, 1000000, ErrorMessage = "El recargo no puede ser negativo.")]
    public decimal Recargo { get; init; } = 0;

    [Range(0, 1000000, ErrorMessage = "La bonificación no puede ser negativa.")]
    public decimal Bonificacion { get; init; } = 0;
}
