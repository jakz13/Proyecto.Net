using MediatR;
using UltimaMilla.Application.DTOs.Mobile;

namespace UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ValidarEscaneoCarga;

public record ValidarEscaneoCargaCommand(
    Guid HojaDeRutaId,
    List<string> CodigosBultosEscaneados
) : IRequest<ResultadoVerificacionCargaDto>;
