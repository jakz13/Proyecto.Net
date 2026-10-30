using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Application.Queries.Operador.GestionComercios;

/// <summary>
/// Manejador de la consulta para listar comercios del operador autenticado.
/// </summary>
public class ListarComerciosQueryHandler : IRequestHandler<ListarComerciosQuery, ResultadoPaginadoComerciosDto>
{
    private readonly IComercioOperadorRepository _comercioOperadorRepositorio;
    private readonly ICurrentTenant _inquilinoActual;

    public ListarComerciosQueryHandler(
        IComercioOperadorRepository comercioOperadorRepositorio,
        ICurrentTenant inquilinoActual)
    {
        _comercioOperadorRepositorio = comercioOperadorRepositorio;
        _inquilinoActual = inquilinoActual;
    }

    public async Task<ResultadoPaginadoComerciosDto> Handle(ListarComerciosQuery solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var operadorId = _inquilinoActual.OperadorId;
        if (operadorId == Guid.Empty)
            throw new UnauthorizedAccessException("Operador no autenticado.");

        var paginaNormalizada = Math.Max(solicitud.Pagina, 1);
        var tamanoNormalizado = Math.Clamp(solicitud.TamanoPagina, 1, 100);

        var (elementos, total) = await _comercioOperadorRepositorio.ListarComerciosDelOperadorAsync(
            operadorId,
            solicitud.Busqueda,
            solicitud.SoloActivos,
            paginaNormalizada,
            tamanoNormalizado,
            cancellationToken);

        var dtos = elementos.Select(e => new ComercioDetalleDto(
            e.Comercio.Id,
            e.Comercio.RazonSocial,
            e.Comercio.NombreFantasia,
            e.Comercio.Documento,
            e.Comercio.Email,
            e.Comercio.Telefono,
            e.Comercio.Direccion,
            e.Relacion.EstaActivo,
            e.Relacion.FechaAlta,
            e.Comercio.SelloModificacion.ToString()
        )).ToList();

        return new ResultadoPaginadoComerciosDto(
            dtos,
            total,
            paginaNormalizada,
            tamanoNormalizado
        );
    }
}

