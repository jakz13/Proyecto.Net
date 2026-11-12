using MediatR;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Application.Queries.Operador.GestionTarifas;

/// <summary>
/// Consulta para obtener la versión actualmente vigente del cuadro tarifario del operador.
/// </summary>
public record ObtenerCuadroTarifarioVigenteQuery : IRequest<CuadroTarifarioDetalleDto?>;

public class ObtenerCuadroTarifarioVigenteQueryHandler : IRequestHandler<ObtenerCuadroTarifarioVigenteQuery, CuadroTarifarioDetalleDto?>
{
    private readonly ICuadroTarifarioRepository _cuadroRepositorio;
    private readonly IZonaCoberturaRepository _zonaRepositorio;
    private readonly ICurrentTenant _inquilinoActual;
    private readonly IDistributedCacheService? _cacheService;

    public ObtenerCuadroTarifarioVigenteQueryHandler(
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

    public async Task<CuadroTarifarioDetalleDto?> Handle(ObtenerCuadroTarifarioVigenteQuery solicitud, CancellationToken cancellationToken)
    {
        if (_inquilinoActual.OperadorId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Operador no autenticado.");
        }

        var operadorId = _inquilinoActual.OperadorId;
        var ahoraUtc = DateTime.UtcNow;
        var claveCache = ClavesCache.CuadroTarifarioVigente(operadorId);

        if (_cacheService is not null)
        {
            var enCache = await _cacheService.GetAsync<CuadroTarifarioDetalleDto>(claveCache, cancellationToken);
            if (enCache is not null
                && enCache.OperadorId == operadorId
                && enCache.VigenteDesde <= ahoraUtc
                && (!enCache.VigenteHasta.HasValue || enCache.VigenteHasta.Value >= ahoraUtc))
            {
                return enCache;
            }
        }

        var vigente = await _cuadroRepositorio.ObtenerVigenteAsync(operadorId, ahoraUtc, cancellationToken);
        if (vigente == null)
        {
            return null;
        }

        var zonas = await _zonaRepositorio.ListarZonasDelOperadorAsync(operadorId, null, null, cancellationToken);
        var mapaNombresZonas = zonas.ToDictionary(z => z.Id, z => z.Nombre);

        var resultado = new CuadroTarifarioDetalleDto(
            vigente.Id,
            vigente.OperadorId,
            vigente.Version,
            vigente.VigenteDesde,
            vigente.VigenteHasta,
            vigente.Comentario,
            true,
            vigente.ReglasTarifa.Select(r => new ReglaTarifaDetalleDto(
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
        );

        if (_cacheService is not null)
        {
            await _cacheService.SetAsync(claveCache, resultado, ClavesCache.ExpiracionCuadroTarifarioVigente, cancellationToken);
        }

        return resultado;
    }
}
