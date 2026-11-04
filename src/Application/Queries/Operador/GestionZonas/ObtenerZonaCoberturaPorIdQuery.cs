using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Queries.Operador.GestionZonas;

/// <summary>
/// Consulta para obtener el detalle de una zona de cobertura por su identificador (UC-02).
/// </summary>
public record ObtenerZonaCoberturaPorIdQuery(Guid Id) : IRequest<ZonaCoberturaDetalleDto>;

