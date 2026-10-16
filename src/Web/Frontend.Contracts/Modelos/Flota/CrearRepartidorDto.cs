using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.Flota;

/// <summary>
/// Parámetros requeridos para dar de alta a un repartidor y opcionalmente asociarlo a un vehículo (RF-06).
/// </summary>
public record CrearRepartidorDto
{
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

    public string Estado { get; init; } = "Disponible";
}
