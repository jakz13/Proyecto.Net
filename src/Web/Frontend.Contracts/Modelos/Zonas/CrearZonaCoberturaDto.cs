using System.ComponentModel.DataAnnotations;
using UltimaMilla.Frontend.Contracts.Validaciones;

namespace UltimaMilla.Frontend.Contracts.Modelos.Zonas;

/// <summary>
/// Modelo para el alta de una zona de cobertura geográfica y sus franjas horarias.
/// </summary>
public record CrearZonaCoberturaDto
{
    [Required(ErrorMessage = "El nombre de la zona es obligatorio.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre de la zona debe tener entre 3 y 100 caracteres.")]
    [SinMarcado]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "Debe definir el polígono geográfico de la zona.")]
    [PoligonoValido]
    public string PoligonoGeografico { get; init; } = string.Empty;

    [Required(ErrorMessage = "El color de representación en mapa es obligatorio.")]
    [ColorHexValido]
    public string ColorHex { get; init; } = "#2563eb";

    /// <summary>
    /// Franjas horarias iniciales a asociar a la zona.
    /// </summary>
    public List<CrearFranjaHorariaDto> FranjasHorarias { get; init; } = new();
}
