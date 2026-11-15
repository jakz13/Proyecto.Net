using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.Funcionalidades;

/// <summary>Alta de una empresa de envíos desde la plataforma. El administrador inicial es obligatorio: con esa cuenta la empresa empieza a crear sus demás perfiles.</summary>
public class CrearOperadorDto
{
    [Required(ErrorMessage = "La razón social es obligatoria.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "La razón social debe tener entre 3 y 200 caracteres.")]
    public string RazonSocial { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre comercial es obligatorio.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "El nombre comercial debe tener entre 3 y 200 caracteres.")]
    public string NombreComercial { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre del administrador inicial es obligatorio.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "El nombre del administrador debe tener entre 3 y 150 caracteres.")]
    public string AdministradorNombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo del administrador inicial es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
    [StringLength(200)]
    public string AdministradorEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña del administrador inicial es obligatoria.")]
    public string AdministradorPassword { get; set; } = string.Empty;
}
