namespace UltimaMilla.Application.DTOs.Operador;

/// <summary>
/// DTO para la definición de una franja horaria dentro de una zona de cobertura.
/// </summary>
public record CrearFranjaHorariaDto(
    DayOfWeek DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int CapacidadMaximaEnvios = 30
);

/// <summary>
/// DTO con la información detallada de una franja horaria existente.
/// </summary>
public record FranjaHorariaDetalleDto(
    Guid Id,
    Guid ZonaCoberturaId,
    DayOfWeek DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int CapacidadMaximaEnvios,
    bool Activa
);

/// <summary>
/// DTO con la información detallada de una zona de cobertura y sus franjas asociadas.
/// </summary>
public record ZonaCoberturaDetalleDto(
    Guid Id,
    Guid OperadorId,
    string Nombre,
    string PoligonoGeografico,
    string ColorHex,
    bool Activa,
    DateTime FechaCreacion,
    IReadOnlyList<FranjaHorariaDetalleDto> FranjasHorarias
);

