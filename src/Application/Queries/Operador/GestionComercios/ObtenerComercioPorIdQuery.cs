using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Queries.Operador.GestionComercios;

/// <summary>
/// Consulta para obtener el detalle de un comercio específico del operador (UC-01).
/// </summary>
public record ObtenerComercioPorIdQuery(Guid Id) : IRequest<ComercioDetalleDto>;

