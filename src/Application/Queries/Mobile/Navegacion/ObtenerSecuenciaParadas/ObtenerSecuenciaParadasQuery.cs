using MediatR;
using UltimaMilla.Application.DTOs.Mobile;

namespace UltimaMilla.Application.Queries.Mobile.Navegacion.ObtenerSecuenciaParadas;

public record ObtenerSecuenciaParadasQuery(Guid HojaDeRutaId) : IRequest<List<ParadaNavegacionDto>>;
