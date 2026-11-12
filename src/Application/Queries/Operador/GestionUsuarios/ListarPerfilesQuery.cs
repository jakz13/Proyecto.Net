using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Queries.Operador.GestionUsuarios;

/// <summary>
/// Consulta para listar todos los perfiles o roles disponibles en el sistema.
/// </summary>
public record ListarPerfilesQuery : IRequest<IReadOnlyList<PerfilDetalleDto>>;

