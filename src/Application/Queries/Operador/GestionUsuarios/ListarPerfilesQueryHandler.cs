using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Application.Queries.Operador.GestionUsuarios;

/// <summary>
/// Manejador para listar los perfiles disponibles para su asignación a usuarios.
/// </summary>
public class ListarPerfilesQueryHandler : IRequestHandler<ListarPerfilesQuery, IReadOnlyList<PerfilDetalleDto>>
{
    private readonly IUsuarioRepository _usuarioRepositorio;

    public ListarPerfilesQueryHandler(IUsuarioRepository usuarioRepositorio)
    {
        _usuarioRepositorio = usuarioRepositorio;
    }

    public async Task<IReadOnlyList<PerfilDetalleDto>> Handle(ListarPerfilesQuery solicitud, CancellationToken cancellationToken)
    {
        var perfiles = await _usuarioRepositorio.ListarPerfilesAsync(cancellationToken);
        return perfiles.Select(p => new PerfilDetalleDto(p.Id, p.Nombre, p.Descripcion)).ToList();
    }
}

