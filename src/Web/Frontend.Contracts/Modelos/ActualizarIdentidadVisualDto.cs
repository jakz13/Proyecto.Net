using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos;

/// <summary>
/// Modelo de datos con validaciones para actualizar la identidad visual del operador logístico (UC-06, RF-05).
/// </summary>
public class ActualizarIdentidadVisualDto
{
    [Required(ErrorMessage = "El nombre comercial de la marca es obligatorio.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre comercial debe tener entre 3 y 100 caracteres.")]
    public string NombreComercial { get; set; } = string.Empty;

    [Required(ErrorMessage = "El color primario es obligatorio.")]
    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "El color primario debe ser un código hexadecimal válido (ej: #1D4ED8).")]
    public string ColorPrimario { get; set; } = "#1D4ED8";

    [Required(ErrorMessage = "El color secundario es obligatorio.")]
    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "El color secundario debe ser un código hexadecimal válido (ej: #F97316).")]
    public string ColorSecundario { get; set; } = "#F97316";

    /// <summary>
    /// URL o cadena en formato de imagen/SVG para el logotipo oficial.
    /// </summary>
    public string? LogoUrl { get; set; }

    [Required(ErrorMessage = "El correo de contacto o soporte es obligatorio.")]
    [EmailAddress(ErrorMessage = "Debe ingresar un formato de correo electrónico válido.")]
    [StringLength(100, ErrorMessage = "El correo no puede exceder 100 caracteres.")]
    public string EmailContacto { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono de contacto oficial es obligatorio.")]
    [StringLength(50, MinimumLength = 6, ErrorMessage = "El teléfono debe tener entre 6 y 50 caracteres.")]
    public string TelefonoContacto { get; set; } = string.Empty;

    /// <summary>
    /// Dirección web del portal o sitio institucional del operador.
    /// </summary>
    [StringLength(150, ErrorMessage = "La dirección web no puede exceder 150 caracteres.")]
    public string? SitioWeb { get; set; }

    /// <summary>
    /// Texto de cabecera o firma predeterminada en notificaciones dirigidas al destinatario final.
    /// </summary>
    [StringLength(300, ErrorMessage = "El mensaje para comunicaciones no puede exceder 300 caracteres.")]
    public string? MensajeComunicaciones { get; set; }
}
