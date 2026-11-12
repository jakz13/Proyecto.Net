using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Queries.Operador.GestionZonas;

/// <summary>
/// Consulta para obtener el detalle de una zona de cobertura por su identificador.
/// </summary>
public record ObtenerZonaCoberturaPorIdQuery(Guid Id) : IRequest<ZonaCoberturaDetalleDto>;

