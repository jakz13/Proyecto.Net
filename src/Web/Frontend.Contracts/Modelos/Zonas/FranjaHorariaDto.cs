namespace UltimaMilla.Frontend.Contracts.Modelos.Zonas;

/// <summary>
/// Representa una franja horaria de entrega asociada a una zona de cobertura (UC-02, RF-02).
/// </summary>
public record FranjaHorariaDto
{
    public Guid Id { get; init; }
    public Guid ZonaCoberturaId { get; init; }
    public DayOfWeek DiaSemana { get; init; }
    public TimeOnly HoraInicio { get; init; }
    public TimeOnly HoraFin { get; init; }
    public int CapacidadMaximaEnvios { get; init; } = 30;
    public bool Activa { get; init; } = true;

    /// <summary>
    /// Nombre en español del día de la semana.
    /// </summary>
    public string NombreDia => DiaSemana switch
    {
        DayOfWeek.Monday => "Lunes",
        DayOfWeek.Tuesday => "Martes",
        DayOfWeek.Wednesday => "Miércoles",
        DayOfWeek.Thursday => "Jueves",
        DayOfWeek.Friday => "Viernes",
        DayOfWeek.Saturday => "Sábado",
        DayOfWeek.Sunday => "Domingo",
        _ => DiaSemana.ToString()
    };

    /// <summary>
    /// Representación legible del rango de horario (ej: "09:00 - 13:00").
    /// </summary>
    public string HorarioTexto => $"{HoraInicio:HH\\:mm} a {HoraFin:HH\\:mm} hs";
}
