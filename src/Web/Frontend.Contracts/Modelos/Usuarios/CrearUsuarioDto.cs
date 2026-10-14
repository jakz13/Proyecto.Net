using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.Usuarios;

/// <summary>
/// Modelo para el alta de un nuevo usuario en el sistema (UC-01, RF-31).
/// </summary>
public record CrearUsuarioDto
{
    [Required(ErrorMessage = "El nombre del usuario es obligatorio.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "La contraseña provisional es obligatoria.")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "Debe asignar un perfil/rol al usuario.")]
    public Guid PerfilId { get; init; }

    /// <summary>
    /// Opcional: Si el usuario pertenece a un comercio cliente puntual en lugar del operador.
    /// </summary>
    public Guid? ComercioId { get; init; }
}
