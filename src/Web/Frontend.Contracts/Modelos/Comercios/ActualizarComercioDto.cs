using System.ComponentModel.DataAnnotations;
using UltimaMilla.Frontend.Contracts.Validaciones;

namespace UltimaMilla.Frontend.Contracts.Modelos.Comercios;

/// <summary>
/// Modelo de datos para actualizar un comercio existente.
/// Incluye SelloModificacion para control de concurrencia optimista (evitar colisiones 409).
/// </summary>
public record ActualizarComercioDto
{
    [Required(ErrorMessage = "La razón social es obligatoria.")]
    public string RazonSocial { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre de fantasía es obligatorio.")]
    public string NombreFantasia { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailValido]
    [StringLength(200, ErrorMessage = "El correo no puede superar los 200 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono de contacto es obligatorio.")]
    [TelefonoNueveDigitos]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "La dirección es obligatoria.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "La dirección debe tener entre 5 y 200 caracteres.")]
    [DireccionValida]
    public string Direccion { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    /// <summary>
    /// Sello de modificación recibido previamente; el backend validará coincidencia y emitirá 409 en conflicto.
    /// </summary>
    [Required(ErrorMessage = "El sello de modificación es obligatorio para garantizar concurrencia.")]
    public string SelloModificacion { get; set; } = string.Empty;
}
