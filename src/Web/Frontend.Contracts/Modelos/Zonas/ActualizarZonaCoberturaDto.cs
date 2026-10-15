using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.Zonas;

/// <summary>
/// Modelo para modificar una zona de cobertura y actualizar sus franjas horarias (UC-02, RF-02).
/// </summary>
public record ActualizarZonaCoberturaDto
{
    [Required(ErrorMessage = "El nombre de la zona es obligatorio.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre de la zona debe tener entre 3 y 100 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "Debe definir el polígono geográfico de la zona.")]
    public string PoligonoGeografico { get; init; } = string.Empty;

    [Required(ErrorMessage = "El color de representación en mapa es obligatorio.")]
    public string ColorHex { get; init; } = "#2563eb";

    public bool Activa { get; init; } = true;

    /// <summary>
    /// Franjas horarias actualizadas de la zona.
    /// </summary>
    public List<CrearFranjaHorariaDto> FranjasHorarias { get; init; } = new();
}
