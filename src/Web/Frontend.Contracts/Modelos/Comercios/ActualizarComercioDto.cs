using System.ComponentModel.DataAnnotations;

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
    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono de contacto es obligatorio.")]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "La dirección es obligatoria.")]
    public string Direccion { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    /// <summary>
    /// Sello de modificación recibido previamente; el backend validará coincidencia y emitirá 409 en conflicto.
    /// </summary>
    [Required(ErrorMessage = "El sello de modificación es obligatorio para garantizar concurrencia.")]
    public string SelloModificacion { get; set; } = string.Empty;
}
