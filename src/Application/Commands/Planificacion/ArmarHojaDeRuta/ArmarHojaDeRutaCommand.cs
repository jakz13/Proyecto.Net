using MediatR;

namespace UltimaMilla.Application.Commands.Planificacion.ArmarHojaDeRuta;

/// <summary>
/// Armado y liberación de una hoja de ruta para un repartidor, un vehículo y una jornada. La hoja de ruta puede ser mixta:
/// entregas de envíos y retiros en domicilio. Con SoloValidar se aplican las mismas restricciones pero no se asigna ni se guarda nada.
/// </summary>
public record ArmarHojaDeRutaCommand(
    Guid RepartidorId,
    Guid VehiculoId,
    DateOnly Fecha,
    IReadOnlyList<Guid> EnviosIds,
    bool SoloValidar = false,
    IReadOnlyList<Guid>? RetirosIds = null
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
    IReadOnlyList<EnvioExcluidoDto> Excluidos,
    decimal PicoPeso = 0,
    decimal PicoVolumen = 0);

/// <summary>Parada armada. En una parada de retiro, <c>EnvioId</c> es Guid.Empty y <c>RetiroId</c> identifica el retiro.</summary>
public record ParadaArmadaDto(int Orden, Guid ParadaId, Guid EnvioId, string CodigoSeguimiento, string Tipo = "Entrega", Guid? RetiroId = null);

/// <summary>Envío o retiro que el despachador pidió incluir pero no pudo asignarse, con el motivo.</summary>
public record EnvioExcluidoDto(Guid EnvioId, string Motivo);
