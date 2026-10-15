using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.Zonas;

/// <summary>
/// Modelo para el alta de una zona de cobertura geográfica y sus franjas horarias (UC-02, RF-02).
/// </summary>
public record CrearZonaCoberturaDto
{
    [Required(ErrorMessage = "El nombre de la zona es obligatorio.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre de la zona debe tener entre 3 y 100 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "Debe definir el polígono geográfico de la zona.")]
    public string PoligonoGeografico { get; init; } = string.Empty;

    [Required(ErrorMessage = "El color de representación en mapa es obligatorio.")]
    public string ColorHex { get; init; } = "#2563eb";

    /// <summary>
    /// Franjas horarias iniciales a asociar a la zona.
    /// </summary>
    public List<CrearFranjaHorariaDto> FranjasHorarias { get; init; } = new();
}
