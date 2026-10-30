using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Application.Queries.Operador.GestionUsuarios;

/// <summary>
/// Manejador de la consulta para listar usuarios con resolución de nombres de perfil y comercio asociado.
/// </summary>
public class ListarUsuariosQueryHandler : IRequestHandler<ListarUsuariosQuery, ResultadoPaginadoUsuariosDto>
{
    private readonly IUsuarioRepository _usuarioRepositorio;
    private readonly ICurrentTenant _inquilinoActual;

    public ListarUsuariosQueryHandler(
        IUsuarioRepository usuarioRepositorio,
        ICurrentTenant inquilinoActual)
    {
        _usuarioRepositorio = usuarioRepositorio;
        _inquilinoActual = inquilinoActual;
    }

    public async Task<ResultadoPaginadoUsuariosDto> Handle(ListarUsuariosQuery solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var operadorId = _inquilinoActual.OperadorId;
        if (operadorId == Guid.Empty)
            throw new UnauthorizedAccessException("Operador no autenticado.");

        var paginaNormalizada = Math.Max(solicitud.Pagina, 1);
        var tamanoNormalizado = Math.Clamp(solicitud.TamanoPagina, 1, 100);

        var (usuarios, total) = await _usuarioRepositorio.ListarUsuariosDelOperadorAsync(
            operadorId,
            solicitud.Busqueda,
            solicitud.PerfilId,
            solicitud.SoloActivos,
            paginaNormalizada,
            tamanoNormalizado,
            cancellationToken);

        var listaUsuarios = usuarios.ToList();
        var idsPerfiles = listaUsuarios.Select(u => u.PerfilId).Distinct().ToList();
        var idsComercios = listaUsuarios.Where(u => u.ComercioId.HasValue).Select(u => u.ComercioId!.Value).Distinct().ToList();

        var mapaPerfiles = await _usuarioRepositorio.ObtenerNombresPerfilesAsync(idsPerfiles, cancellationToken);
        var mapaComercios = await _usuarioRepositorio.ObtenerNombresComerciosAsync(idsComercios, cancellationToken);

        var dtos = listaUsuarios.Select(u => new UsuarioDetalleDto(
            u.Id,
            u.Nombre,
            u.Email,
            u.PerfilId,
            mapaPerfiles.GetValueOrDefault(u.PerfilId, string.Empty),
            u.OperadorId,
            u.ComercioId,
            u.ComercioId.HasValue ? mapaComercios.GetValueOrDefault(u.ComercioId.Value) : null,
            u.Activo,
            u.FechaAlta
        )).ToList();

        return new ResultadoPaginadoUsuariosDto(
            dtos,
            total,
            paginaNormalizada,
            tamanoNormalizado
        );
    }
}

