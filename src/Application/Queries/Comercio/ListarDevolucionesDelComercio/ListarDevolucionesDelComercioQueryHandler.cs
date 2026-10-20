using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Comercio.ListarDevolucionesDelComercio;

public class ListarDevolucionesDelComercioQueryHandler : IRequestHandler<ListarDevolucionesDelComercioQuery, PaginaDevolucionesDto>
{
    private const int TamanoMaximoPagina = 100;

    private readonly IEnvioRepository _envioRepository;
    private readonly IDevolucionRepository _devolucionRepository;
    private readonly ICurrentTenant _currentTenant;

    public ListarDevolucionesDelComercioQueryHandler(
        IEnvioRepository envioRepository,
        IDevolucionRepository devolucionRepository,
        ICurrentTenant currentTenant)
    {
        _envioRepository = envioRepository;
        _devolucionRepository = devolucionRepository;
        _currentTenant = currentTenant;
    }

    public async Task<PaginaDevolucionesDto> Handle(ListarDevolucionesDelComercioQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var comercioId = _currentTenant.ComercioOperadorId
            ?? throw new UnauthorizedAccessException("Acceso denegado: el usuario no pertenece a un comercio.");

        if (request.Pagina < 1)
            throw new DomainValidationException("La página debe ser mayor o igual a 1.");

        if (request.TamanoPagina < 1 || request.TamanoPagina > TamanoMaximoPagina)
            throw new DomainValidationException($"El tamaño de página debe estar entre 1 y {TamanoMaximoPagina}.");

        var estado = request.SoloCerradas ? EstadoEnvio.Devuelto : EstadoEnvio.EnDevolucion;
        var filtro = new FiltroEnviosComercio(estado, null, null, null, request.Pagina, request.TamanoPagina);

        var (envios, total) = await _envioRepository.BuscarPorComercioAsync(_currentTenant.OperadorId, comercioId, filtro, cancellationToken);
        var devoluciones = (await _devolucionRepository.GetByEnviosIdsAsync(envios.Select(e => e.Id), cancellationToken))
            .ToDictionary(d => d.EnvioId);

        var items = envios.Select(e =>
        {
            devoluciones.TryGetValue(e.Id, out var devolucion);
            return new DevolucionComercioDto(
                e.Id,
                e.CodigoSeguimientoPublico,
                e.NombreDestinatario,
                e.EstadoActual.ToString(),
                devolucion?.Estado ?? (e.EstadoActual == EstadoEnvio.Devuelto ? Devolucion.EstadoDevuelto : Devolucion.EstadoEnDevolucion),
                devolucion?.FechaInicio,
                devolucion?.FechaRendicion,
                e.EstadoActual == EstadoEnvio.EnDevolucion);
        }).ToList();

        return new PaginaDevolucionesDto(items, total, request.Pagina, request.TamanoPagina, (int)Math.Ceiling(total / (double)request.TamanoPagina));
    }
}
