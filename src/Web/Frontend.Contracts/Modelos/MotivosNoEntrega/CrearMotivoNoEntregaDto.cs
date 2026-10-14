using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.MotivosNoEntrega;

/// <summary>
/// Modelo para dar de alta un nuevo motivo de no entrega en el catálogo (UC-05).
/// </summary>
public class CrearMotivoNoEntregaDto
{
    [Required(ErrorMessage = "El código del motivo es obligatorio.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "El código debe tener entre 3 y 30 caracteres.")]
    [RegularExpression(@"^[A-Za-z0-9_]+$", ErrorMessage = "El código solo puede contener letras mayúsculas, números y guiones bajos (ej: CLIENTE_AUSENTE).")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción del motivo es obligatoria.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "La descripción debe tener entre 5 y 200 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Si inicia activo de inmediato para sincronización con la app móvil (RF-21).
    /// </summary>
    public bool IniciarActivo { get; set; } = true;
}
