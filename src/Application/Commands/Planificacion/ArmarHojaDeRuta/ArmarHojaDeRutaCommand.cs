using MediatR;

namespace UltimaMilla.Application.Commands.Planificacion.ArmarHojaDeRuta;

/// <summary>Armado y liberación de una hoja de ruta para un repartidor, un vehículo y una jornada.</summary>
public record ArmarHojaDeRutaCommand(
    Guid RepartidorId,
    Guid VehiculoId,
    DateOnly Fecha,
    IReadOnlyList<Guid> EnviosIds
) : IRequest<ArmarHojaDeRutaResultDto>;

public record ArmarHojaDeRutaResultDto(
    Guid HojaDeRutaId,
    string Estado,
    DateOnly Fecha,
    Guid RepartidorId,
    Guid VehiculoId,
    decimal PesoTotal,
    decimal VolumenTotal,
    IReadOnlyList<ParadaArmadaDto> Paradas,
    IReadOnlyList<EnvioExcluidoDto> Excluidos);

public record ParadaArmadaDto(int Orden, Guid ParadaId, Guid EnvioId, string CodigoSeguimiento);

/// <summary>Envío que el despachador pidió incluir pero no pudo asignarse, con el motivo.</summary>
public record EnvioExcluidoDto(Guid EnvioId, string Motivo);
