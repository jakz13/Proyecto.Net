using System.ComponentModel.DataAnnotations;
using UltimaMilla.Frontend.Contracts.Validaciones;

namespace UltimaMilla.Frontend.Contracts.Modelos.Usuarios;

/// <summary>
/// Modelo para el alta de un nuevo usuario en el sistema con campos separados de Nombre y Apellido.
/// </summary>
public record CrearUsuarioDto
{
    [Required(ErrorMessage = "El nombre del usuario es obligatorio.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 50 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "El apellido del usuario es obligatorio.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "El apellido debe tener entre 2 y 50 caracteres.")]
    public string Apellido { get; init; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailValido]
    [StringLength(200, ErrorMessage = "El correo no puede superar los 200 caracteres.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "La contraseña provisional es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "Debe asignar un perfil/rol al usuario.")]
    public Guid PerfilId { get; init; }

    /// <summary>
    /// Opcional: Si el usuario pertenece a un comercio cliente puntual en lugar del operador.
    /// </summary>
    public Guid? ComercioId { get; set; }

    /// <summary>
    /// Cédula o documento de identidad cuando el rol asignado es Repartidor.
    /// </summary>
    [RegularExpression(@"^\d{8}$", ErrorMessage = "La cédula debe tener exactamente 8 dígitos, solo números (sin puntos ni guiones).")]
    public string? Documento { get; init; }

    /// <summary>
    /// Teléfono celular de contacto cuando el rol asignado es Repartidor.
    /// </summary>
    [TelefonoNueveDigitos]
    public string? Telefono { get; init; }

    /// <summary>
    /// Vehículo asignado opcional cuando el rol asignado es Repartidor.
    /// </summary>
    public Guid? VehiculoAsignadoId { get; init; }

    /// <summary>
    /// Devuelve el nombre completo formateado.
    /// </summary>
    public string NombreCompleto => string.IsNullOrWhiteSpace(Apellido)
        ? (Nombre ?? string.Empty).Trim()
        : $"{(Nombre ?? string.Empty).Trim()} {Apellido.Trim()}";
}
