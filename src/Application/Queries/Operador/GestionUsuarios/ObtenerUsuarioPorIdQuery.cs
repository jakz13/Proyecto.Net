using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Queries.Operador.GestionUsuarios;

/// <summary>
/// Consulta para obtener el detalle de un usuario puntual del operador.
/// </summary>
public record ObtenerUsuarioPorIdQuery(Guid Id) : IRequest<UsuarioDetalleDto>;

