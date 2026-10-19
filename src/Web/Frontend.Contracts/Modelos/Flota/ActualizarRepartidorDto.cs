using System.ComponentModel.DataAnnotations;

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
    [EmailAddress(ErrorMessage = "El correo electrónico no tiene un formato válido.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "El documento es obligatorio.")]
    [StringLength(30, MinimumLength = 5, ErrorMessage = "El documento debe tener entre 5 y 30 caracteres.")]
    public string Documento { get; init; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(30, MinimumLength = 6, ErrorMessage = "El teléfono debe tener entre 6 y 30 caracteres.")]
    public string Telefono { get; init; } = string.Empty;

    public Guid? VehiculoAsignadoId { get; init; }

    [Required(ErrorMessage = "El estado es obligatorio.")]
    public string Estado { get; init; } = "Disponible";
}
