using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.Tarifas;

/// <summary>
/// Modelo para dar de alta una regla dentro de una nueva versión del cuadro tarifario.
/// </summary>
public record CrearReglaTarifaDto
{
    /// <summary>
    /// Opcional: Identificador de la zona de cobertura a la que aplica la tarifa. Si es nulo, aplica para todas las zonas.
    /// </summary>
    public Guid? ZonaCoberturaId { get; init; }

    // Sin valor por defecto: un cuerpo {} no debe fabricar una regla "completa" (D-04 de la auditoría del 14/11). El formulario siempre envía la modalidad.
    [Required(ErrorMessage = "La modalidad de servicio es obligatoria.")]
    public string ModalidadServicio { get; init; } = string.Empty;

    [Range(0, 10000, ErrorMessage = "El peso mínimo no puede ser negativo.")]
    public decimal PesoDesde { get; init; } = 0;

    [Range(0.01, 10000, ErrorMessage = "El peso máximo debe ser mayor a cero.")]
    public decimal PesoHasta { get; init; } = 5;

    // El tope es el "sin límite" que usa el cuadro real (1.000.000.000): con un rango de hasta 1.000 la pantalla rechazaba la propia tarifa vigente (D-07, 14/11)
    [Range(0, 1000000000, ErrorMessage = "El volumen mínimo debe estar entre 0 y 1.000.000.000.")]
    public decimal VolumenDesde { get; init; } = 0;

    [Range(0.001, 1000000000, ErrorMessage = "El volumen máximo debe estar entre 0,001 y 1.000.000.000.")]
    public decimal VolumenHasta { get; init; } = 0.05m;

    [Range(0, 1000000, ErrorMessage = "El precio base no puede ser negativo.")]
    public decimal PrecioBase { get; init; } = 180;

    [Range(0, 1000000, ErrorMessage = "El recargo no puede ser negativo.")]
    public decimal Recargo { get; init; } = 0;

    [Range(0, 1000000, ErrorMessage = "La bonificación no puede ser negativa.")]
    public decimal Bonificacion { get; init; } = 0;
}
