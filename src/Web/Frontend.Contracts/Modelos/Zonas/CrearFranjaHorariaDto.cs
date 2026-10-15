using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Frontend.Contracts.Modelos.Zonas;

/// <summary>
/// Modelo para crear o actualizar una franja horaria de entrega en una zona (UC-02, RF-02).
/// </summary>
public record CrearFranjaHorariaDto
{
    [Required(ErrorMessage = "El día de la semana es obligatorio.")]
    public DayOfWeek DiaSemana { get; init; } = DayOfWeek.Monday;

    [Required(ErrorMessage = "La hora de inicio es obligatoria.")]
    public TimeOnly HoraInicio { get; init; } = new(9, 0);

    [Required(ErrorMessage = "La hora de fin es obligatoria.")]
    public TimeOnly HoraFin { get; init; } = new(13, 0);

    [Range(1, 500, ErrorMessage = "La capacidad máxima debe ser de al menos 1 envío.")]
    public int CapacidadMaximaEnvios { get; init; } = 30;
}
