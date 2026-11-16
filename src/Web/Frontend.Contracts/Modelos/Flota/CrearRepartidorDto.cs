using System.ComponentModel.DataAnnotations;
using UltimaMilla.Frontend.Contracts.Validaciones;

namespace UltimaMilla.Frontend.Contracts.Modelos.Flota;

/// <summary>
/// Parámetros requeridos para dar de alta a un repartidor y opcionalmente asociarlo a un vehículo.
/// </summary>
public record CrearRepartidorDto
{
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

    public string Estado { get; init; } = "Disponible";

    /// <summary>
    /// Contraseña de acceso a la app móvil del repartidor. Obligatoria (mínimo 8 caracteres): no existe una contraseña por defecto.
    /// </summary>
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    public string? Password { get; init; }
}
