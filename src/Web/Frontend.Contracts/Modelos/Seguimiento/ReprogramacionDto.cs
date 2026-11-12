namespace UltimaMilla.Frontend.Contracts.Modelos.Seguimiento;

/// <summary>
/// Modelos para la consulta de franjas horarias y evaluación de reprogramación de entrega.
/// </summary>
public record FranjaDisponibleDto(
    Guid Id,
    string DiaSemana,
    string HoraInicio,
    string HoraFin,
    bool EsLaActual
);

public record FranjasDisponiblesDto(
    bool PuedeReprogramar,
    IReadOnlyList<FranjaDisponibleDto> Franjas
);

public record ResultadoReprogramacionDto(
    bool Aceptada,
    string Estado,
    string? Motivo,
    string? FranjaAsignada
);

