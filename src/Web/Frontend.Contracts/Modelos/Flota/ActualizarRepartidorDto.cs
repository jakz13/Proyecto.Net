using System.ComponentModel.DataAnnotations;
using UltimaMilla.Frontend.Contracts.Validaciones;

namespace UltimaMilla.Frontend.Contracts.Modelos.Flota;

/// <summary>
/// Parámetros para actualizar información de contacto, estado o vehículo de un repartidor.
/// </summary>
public record ActualizarRepartidorDto
{
    [Required]
    public Guid Id { get; init; }

    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres.")]
    public string NombreCompleto { get; init; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailValido]
    [StringLength(200, ErrorMessage = "El correo no puede superar los 200 caracteres.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "El documento es obligatorio.")]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "La cédula debe tener exactamente 8 dígitos, solo números (sin puntos ni guiones).")]
    public string Documento { get; init; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [TelefonoNueveDigitos]
    public string Telefono { get; init; } = string.Empty;

    public Guid? VehiculoAsignadoId { get; init; }

    [Required(ErrorMessage = "El estado es obligatorio.")]
    public string Estado { get; init; } = "Disponible";

    /// <summary>
    /// Nueva contraseña de acceso a la app móvil (opcional: solo si se desea cambiar).
    /// </summary>
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    public string? NuevaPassword { get; init; }
}
