using MediatR;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Application.Queries.Operador.GestionTarifas;

/// <summary>
/// Consulta para listar el historial completo de versiones de cuadros tarifarios del operador.
/// </summary>
public record ListarVersionesCuadroTarifarioQuery : IRequest<IReadOnlyList<CuadroTarifarioDetalleDto>>;

public class ListarVersionesCuadroTarifarioQueryHandler : IRequestHandler<ListarVersionesCuadroTarifarioQuery, IReadOnlyList<CuadroTarifarioDetalleDto>>
{
    private readonly ICuadroTarifarioRepository _cuadroRepositorio;
    private readonly IZonaCoberturaRepository _zonaRepositorio;
    private readonly ICurrentTenant _inquilinoActual;
    private readonly IDistributedCacheService? _cacheService;

    public ListarVersionesCuadroTarifarioQueryHandler(
        ICuadroTarifarioRepository cuadroRepositorio,
        IZonaCoberturaRepository zonaRepositorio,
        ICurrentTenant inquilinoActual,
        IDistributedCacheService? cacheService = null)
    {
        _cuadroRepositorio = cuadroRepositorio;
        _zonaRepositorio = zonaRepositorio;
        _inquilinoActual = inquilinoActual;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyList<CuadroTarifarioDetalleDto>> Handle(ListarVersionesCuadroTarifarioQuery solicitud, CancellationToken cancellationToken)
    {
        if (_inquilinoActual.OperadorId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Operador no autenticado.");
        }

        var operadorId = _inquilinoActual.OperadorId;
        var claveCache = ClavesCache.CuadroTarifarioHistorial(operadorId);

        if (_cacheService is not null)
        {
            var enCache = await _cacheService.GetAsync<List<CuadroTarifarioDetalleDto>>(claveCache, cancellationToken);
            if (enCache is not null && enCache.All(c => c.OperadorId == operadorId))
            {
                return enCache;
            }
        }

        var cuadros = await _cuadroRepositorio.ListarVersionesAsync(operadorId, cancellationToken);
        var zonas = await _zonaRepositorio.ListarZonasDelOperadorAsync(operadorId, null, null, cancellationToken);
        var mapaNombresZonas = zonas.ToDictionary(z => z.Id, z => z.Nombre);
        var ahoraUtc = DateTime.UtcNow;

        var resultado = cuadros.Select(c => new CuadroTarifarioDetalleDto(
            c.Id,
            c.OperadorId,
            c.Version,
            c.VigenteDesde,
            c.VigenteHasta,
            c.Comentario,
            c.EstaVigenteEn(ahoraUtc),
            c.ReglasTarifa.Select(r => new ReglaTarifaDetalleDto(
                r.Id,
                r.CuadroTarifarioId,
                r.ZonaCoberturaId,
                r.ZonaCoberturaId.HasValue && mapaNombresZonas.TryGetValue(r.ZonaCoberturaId.Value, out var nombre) ? nombre : null,
                r.ModalidadServicio,
                r.PesoDesde,
                r.PesoHasta,
                r.VolumenDesde,
                r.VolumenHasta,
                r.PrecioBase,
                r.Recargo,
                r.Bonificacion,
                r.CalcularPrecioFinal()
            )).ToList()
        )).ToList();

        if (_cacheService is not null)
        {
            await _cacheService.SetAsync(claveCache, resultado, ClavesCache.ExpiracionCuadroTarifarioVigente, cancellationToken);
        }

        return resultado;
    }
}
