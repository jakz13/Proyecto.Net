using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.Usuarios;

/// <summary>
/// Modelo para modificar los datos y perfil de un usuario existente.
/// </summary>
public record ActualizarUsuarioDto
{
    [Required(ErrorMessage = "El nombre del usuario es obligatorio.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 50 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    public string Apellido { get; init; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Debe asignar un perfil/rol al usuario.")]
    public Guid PerfilId { get; init; }

    public bool Activo { get; init; } = true;

    /// <summary>
    /// Cédula o documento de identidad cuando el rol asignado es Repartidor.
    /// </summary>
    [StringLength(30, ErrorMessage = "El documento no puede superar los 30 caracteres.")]
    public string? Documento { get; init; }

    /// <summary>
    /// Teléfono celular de contacto cuando el rol asignado es Repartidor.
    /// </summary>
    [StringLength(30, ErrorMessage = "El teléfono no puede superar los 30 caracteres.")]
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
