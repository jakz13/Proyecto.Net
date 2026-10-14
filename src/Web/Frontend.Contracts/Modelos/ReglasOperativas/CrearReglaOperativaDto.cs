using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.ReglasOperativas;

/// <summary>
/// DTO con validaciones para la publicación de una nueva versión de reglas operativas (UC-04, RF-04).
/// </summary>
public record CrearReglaOperativaDto
{
    [Required(ErrorMessage = "Debe indicar la cantidad máxima de intentos de entrega.")]
    [Range(1, 10, ErrorMessage = "La cantidad máxima de intentos debe estar entre 1 y 10.")]
    [Display(Name = "Cantidad máxima de intentos")]
    public int MaxIntentosEntrega { get; set; } = 3;

    [Required(ErrorMessage = "Debe indicar el plazo entre intentos en horas.")]
    [Range(1, 168, ErrorMessage = "El plazo entre intentos debe estar entre 1 hora y 168 horas (7 días).")]
    [Display(Name = "Plazo entre intentos (horas)")]
    public int HorasEntreIntentos { get; set; } = 24;

    [Required(ErrorMessage = "Debe especificar el tipo de prueba de entrega exigida.")]
    [StringLength(200, ErrorMessage = "El tipo de prueba de entrega no puede superar 200 caracteres.")]
    [Display(Name = "Tipo de prueba de entrega exigida")]
    public string TipoPruebaEntregaExigida { get; set; } = "Firma digital y Foto del paquete en puerta";

    [Required(ErrorMessage = "Debe definir la política de devolución aplicable.")]
    [StringLength(300, ErrorMessage = "La política de devolución no puede superar 300 caracteres.")]
    [Display(Name = "Política de devolución")]
    public string PoliticaDevolucion { get; set; } = "Custodia por 72 horas en depósito central antes de retorno al comercio remitente";

    [Required(ErrorMessage = "Debe definir el plazo SLA para envíos Estándar.")]
    [Range(1, 240, ErrorMessage = "El plazo comprometido estándar debe estar entre 1 y 240 horas.")]
    [Display(Name = "Plazo comprometido Estándar (horas)")]
    public int HorasPlazoComprometidoEstandar { get; set; } = 48;

    [Required(ErrorMessage = "Debe definir el plazo SLA para envíos Urgentes/Express.")]
    [Range(1, 72, ErrorMessage = "El plazo comprometido urgente debe estar entre 1 y 72 horas.")]
    [Display(Name = "Plazo comprometido Urgente (horas)")]
    public int HorasPlazoComprometidoUrgente { get; set; } = 12;

    [Display(Name = "Fecha y hora de inicio de vigencia")]
    public DateTime VigenteDesde { get; set; } = DateTime.UtcNow;

    [StringLength(300, ErrorMessage = "El comentario no puede superar 300 caracteres.")]
    [Display(Name = "Motivo de la nueva versión")]
    public string? Comentario { get; set; }
}
