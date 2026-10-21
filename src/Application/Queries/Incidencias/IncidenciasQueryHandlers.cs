using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Incidencias;

public class ListarIncidenciasQueryHandler : IRequestHandler<ListarIncidenciasQuery, PaginaIncidenciasDto>
{
    private const int TamanoMaximoPagina = 100;

    private readonly IIncidenciaRepository _incidenciaRepository;
    private readonly ICurrentTenant _currentTenant;

    public ListarIncidenciasQueryHandler(IIncidenciaRepository incidenciaRepository, ICurrentTenant currentTenant)
    {
        _incidenciaRepository = incidenciaRepository;
        _currentTenant = currentTenant;
    }

    public async Task<PaginaIncidenciasDto> Handle(ListarIncidenciasQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Pagina < 1)
            throw new DomainValidationException("La página debe ser mayor o igual a 1.");

        if (request.TamanoPagina < 1 || request.TamanoPagina > TamanoMaximoPagina)
            throw new DomainValidationException($"El tamaño de página debe estar entre 1 y {TamanoMaximoPagina}.");

        var (items, total) = await _incidenciaRepository.ListarAsync(
            _currentTenant.OperadorId,
            string.IsNullOrWhiteSpace(request.Estado) ? null : request.Estado.Trim(),
            string.IsNullOrWhiteSpace(request.Tipo) ? null : request.Tipo.Trim(),
            request.Pagina,
            request.TamanoPagina,
            cancellationToken);

        var resumenes = items
            .Select(i => new IncidenciaResumenDto(
                i.Id, i.Tipo, i.Severidad, i.Estado, i.Descripcion, i.EnvioId, i.HojaDeRutaId, i.ResponsableUsuarioId, i.FechaApertura, i.FechaCierre))
            .ToList();

        return new PaginaIncidenciasDto(
            resumenes, total, request.Pagina, request.TamanoPagina, (int)Math.Ceiling(total / (double)request.TamanoPagina));
    }
}

public class ObtenerIncidenciaQueryHandler : IRequestHandler<ObtenerIncidenciaQuery, IncidenciaDetalleDto>
{
    private readonly IIncidenciaRepository _incidenciaRepository;
    private readonly ICurrentTenant _currentTenant;

    public ObtenerIncidenciaQueryHandler(IIncidenciaRepository incidenciaRepository, ICurrentTenant currentTenant)
    {
        _incidenciaRepository = incidenciaRepository;
        _currentTenant = currentTenant;
    }

    public async Task<IncidenciaDetalleDto> Handle(ObtenerIncidenciaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var incidencia = await _incidenciaRepository.GetByIdAsync(request.IncidenciaId, cancellationToken)
            ?? throw new NotFoundException($"Incidencia '{request.IncidenciaId}' no encontrada.");
        if (incidencia.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: la incidencia no corresponde al operador actual.");

        return new IncidenciaDetalleDto(
            incidencia.Id, incidencia.Tipo, incidencia.Severidad, incidencia.Estado, incidencia.Descripcion,
            incidencia.EnvioId, incidencia.HojaDeRutaId, incidencia.ResponsableUsuarioId,
            incidencia.FechaApertura, incidencia.FechaCierre, incidencia.Resolucion,
            incidencia.Notas.OrderBy(n => n.Fecha).Select(n => new NotaIncidenciaDto(n.Fecha, n.UsuarioId, n.Comentario)).ToList());
    }
}
