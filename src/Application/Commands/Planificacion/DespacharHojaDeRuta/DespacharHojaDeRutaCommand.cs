using MediatR;

namespace UltimaMilla.Application.Commands.Planificacion.DespacharHojaDeRuta;

/// <summary>Confirmación de la salida del vehículo por parte del operario de depósito.</summary>
public record DespacharHojaDeRutaCommand(
    Guid HojaDeRutaId,
    string? Observaciones = null
) : IRequest<DespachoResultDto>;

public record DespachoResultDto(
    Guid HojaDeRutaId,
    string Estado,
    DateTime? FechaDespacho,
    int EnviosEnTransito,
    IReadOnlyList<EnvioRetiradoDto> EnviosRetirados,
    bool Idempotente,
    string Mensaje);

/// <summary>Envío retirado de la ruta al despachar porque sus bultos no se cargaron.</summary>
public record EnvioRetiradoDto(Guid EnvioId, string CodigoSeguimiento, string Motivo);
