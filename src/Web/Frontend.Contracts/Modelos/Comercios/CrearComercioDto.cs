using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.Comercios;

/// <summary>
/// Modelo de datos para registrar un nuevo comercio (UC-01).
/// No incluye OperadorId ya que el operador se resuelve en backend mediante el JWT del usuario autenticado.
/// </summary>
public record CrearComercioDto
{
    [Required(ErrorMessage = "La razón social es obligatoria.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "La razón social debe tener entre 3 y 150 caracteres.")]
    public string RazonSocial { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre de fantasía es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre de fantasía debe tener entre 2 y 100 caracteres.")]
    public string NombreFantasia { get; set; } = string.Empty;

    [Required(ErrorMessage = "El RUT es obligatorio.")]
    [RegularExpression(@"^[0-9]{12}$", ErrorMessage = "El RUT debe contener exactamente 12 dígitos numéricos.")]
    public string Rut { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono de contacto es obligatorio.")]
    [Phone(ErrorMessage = "Ingrese un formato de teléfono válido.")]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "La dirección fiscal o de depósito es obligatoria.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "La dirección debe tener entre 5 y 200 caracteres.")]
    public string Direccion { get; set; } = string.Empty;
}
