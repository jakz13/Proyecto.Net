using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.Usuarios;

/// <summary>
/// Modelo para asignar o cambiar el perfil/rol de un usuario (RF-31).
/// </summary>
public record AsignarPerfilDto
{
    [Required(ErrorMessage = "Debe seleccionar un perfil.")]
    public Guid PerfilId { get; init; }
}
