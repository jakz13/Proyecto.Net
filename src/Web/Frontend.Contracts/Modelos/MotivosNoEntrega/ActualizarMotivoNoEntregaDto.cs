using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.MotivosNoEntrega;

/// <summary>
/// Modelo para modificar la descripción o estado de un motivo de no entrega existente (UC-05).
/// </summary>
public class ActualizarMotivoNoEntregaDto
{
    [Required]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "La descripción del motivo es obligatoria.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "La descripción debe tener entre 5 y 200 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    public bool Activo { get; set; }
}
