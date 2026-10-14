using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.Tarifas;

/// <summary>
/// Modelo para publicar una nueva versión del cuadro tarifario (UC-03, RF-03).
/// Al publicarse, la versión anterior se cierra automáticamente con su rango de vigencia.
/// </summary>
public record CrearVersionCuadroTarifarioDto
{
    [Required(ErrorMessage = "La fecha de inicio de vigencia es obligatoria.")]
    public DateTime VigenteDesde { get; init; } = DateTime.UtcNow;

    [StringLength(200, ErrorMessage = "El comentario no puede superar los 200 caracteres.")]
    public string? Comentario { get; init; }

    /// <summary>
    /// Reglas de precio que conformarán esta nueva versión del cuadro tarifario.
    /// </summary>
    public List<CrearReglaTarifaDto> ReglasTarifa { get; init; } = new();
}
