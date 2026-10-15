using MediatR;
using UltimaMilla.Application.DTOs.Mobile;

namespace UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ConfirmarCarga;

public record ConfirmarCargaYDespacharRutaCommand(
    Guid HojaDeRutaId,
    List<string> CodigosBultosCargados,
    string IdempotencyKey,
    string? ObservacionesDiscrepancia = null
) : IRequest<ConfirmarCargaResultDto>;
