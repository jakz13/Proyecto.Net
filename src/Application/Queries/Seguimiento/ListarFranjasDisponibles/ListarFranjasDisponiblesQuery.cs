using MediatR;

namespace UltimaMilla.Application.Queries.Seguimiento.ListarFranjasDisponibles;

/// <summary>Franjas entre las que el destinatario puede elegir al reprogramar. Null si el código no existe.</summary>
public record ListarFranjasDisponiblesQuery(string? CodigoSeguimiento) : IRequest<FranjasDisponiblesDto?>;

public record FranjasDisponiblesDto(bool PuedeReprogramar, IReadOnlyList<FranjaDisponibleDto> Franjas);

public record FranjaDisponibleDto(Guid Id, string DiaSemana, string HoraInicio, string HoraFin, bool EsLaActual);
