using MediatR;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Application.Queries.Operador.GestionTarifas;

/// <summary>
/// Consulta para obtener el detalle de una versión específica del cuadro tarifario por su identificador (UC-03, RNF 6.7).
/// </summary>
public record ObtenerCuadroTarifarioPorIdQuery(Guid Id) : IRequest<CuadroTarifarioDetalleDto>;

public class ObtenerCuadroTarifarioPorIdQueryHandler : IRequestHandler<ObtenerCuadroTarifarioPorIdQuery, CuadroTarifarioDetalleDto>
{
    private readonly ICuadroTarifarioRepository _cuadroRepositorio;
    private readonly IZonaCoberturaRepository _zonaRepositorio;
    private readonly ICurrentTenant _inquilinoActual;
    private readonly IDistributedCacheService? _cacheService;

    public ObtenerCuadroTarifarioPorIdQueryHandler(
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

    public async Task<CuadroTarifarioDetalleDto> Handle(ObtenerCuadroTarifarioPorIdQuery solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (_inquilinoActual.OperadorId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Operador no autenticado.");
        }

        var operadorId = _inquilinoActual.OperadorId;
        var claveCache = ClavesCache.CuadroTarifarioPorId(operadorId, solicitud.Id);

        if (_cacheService is not null)
        {
            var enCache = await _cacheService.GetAsync<CuadroTarifarioDetalleDto>(claveCache, cancellationToken);
            if (enCache is not null && enCache.OperadorId == operadorId && enCache.Id == solicitud.Id)
            {
                return enCache;
            }
        }

        var cuadro = await _cuadroRepositorio.ObtenerPorIdAsync(solicitud.Id, operadorId, cancellationToken);
        if (cuadro == null)
        {
            throw new NotFoundException($"No se encontró la versión del cuadro tarifario con identificador '{solicitud.Id}'.");
        }

        var zonas = await _zonaRepositorio.ListarZonasDelOperadorAsync(operadorId, null, null, cancellationToken);
        var mapaNombresZonas = zonas.ToDictionary(z => z.Id, z => z.Nombre);

        var resultado = new CuadroTarifarioDetalleDto(
            cuadro.Id,
            cuadro.OperadorId,
            cuadro.Version,
            cuadro.VigenteDesde,
            cuadro.VigenteHasta,
            cuadro.Comentario,
            cuadro.EstaVigenteEn(DateTime.UtcNow),
            cuadro.ReglasTarifa.Select(r => new ReglaTarifaDetalleDto(
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
            await _cacheService.SetAsync(claveCache, resultado, ClavesCache.ExpiracionCuadroTarifarioVersion, cancellationToken);
        }

        return resultado;
    }
}
