using MediatR;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Application.Commands.Operador.GestionTarifas;

/// <summary>
/// Manejador para la simulación del cálculo tarifario bajo la versión vigente del operador.
/// </summary>
public class SimularCotizacionTarifaCommandHandler : IRequestHandler<SimularCotizacionTarifaCommand, CotizacionSimuladaDto>
{
    private readonly ICuadroTarifarioRepository _cuadroRepositorio;
    private readonly IZonaCoberturaRepository _zonaRepositorio;
    private readonly ICurrentTenant _inquilinoActual;
    private readonly IDistributedCacheService? _cacheService;

    public SimularCotizacionTarifaCommandHandler(
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

    public async Task<CotizacionSimuladaDto> Handle(SimularCotizacionTarifaCommand solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (_inquilinoActual.OperadorId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Operador no autenticado.");
        }

        var operadorId = _inquilinoActual.OperadorId;
        var ahoraUtc = DateTime.UtcNow;
        var claveCache = ClavesCache.CuadroTarifarioVigente(operadorId);

        CuadroTarifario? vigente = null;
        Dictionary<Guid, string>? mapaNombresZonas = null;

        if (_cacheService is not null)
        {
            var enCache = await _cacheService.GetAsync<CuadroTarifarioDetalleDto>(claveCache, cancellationToken);
            if (enCache is not null
                && enCache.OperadorId == operadorId
                && enCache.VigenteDesde <= ahoraUtc
                && (!enCache.VigenteHasta.HasValue || enCache.VigenteHasta.Value >= ahoraUtc))
            {
                vigente = ReconstruirDominioDesdeCache(enCache);
                mapaNombresZonas = enCache.ReglasTarifa
                    .Where(r => r.ZonaCoberturaId.HasValue && !string.IsNullOrWhiteSpace(r.NombreZona))
                    .GroupBy(r => r.ZonaCoberturaId!.Value)
                    .ToDictionary(g => g.Key, g => g.First().NombreZona!);
            }
        }

        if (vigente is null)
        {
            vigente = await _cuadroRepositorio.ObtenerVigenteAsync(operadorId, ahoraUtc, cancellationToken);
            if (vigente != null)
            {
                var zonasDelOperador = await _zonaRepositorio.ListarZonasDelOperadorAsync(operadorId, null, null, cancellationToken);
                mapaNombresZonas = zonasDelOperador.ToDictionary(z => z.Id, z => z.Nombre);

                if (_cacheService is not null)
                {
                    var dtoParaCache = new CuadroTarifarioDetalleDto(
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
                            r.ZonaCoberturaId.HasValue && mapaNombresZonas.TryGetValue(r.ZonaCoberturaId.Value, out var n) ? n : null,
                            r.ModalidadServicio,
                            r.PesoDesde,
                            r.PesoHasta,
                            r.VolumenDesde,
                            r.VolumenHasta,
                            r.PrecioBase,
                            r.Recargo,
                            r.Bonificacion,
                            r.CalcularPrecioFinal()
                        )).ToList());

                    await _cacheService.SetAsync(claveCache, dtoParaCache, ClavesCache.ExpiracionCuadroTarifarioVigente, cancellationToken);
                }
            }
        }

        if (vigente == null || vigente.ReglasTarifa.Count == 0)
        {
            return new CotizacionSimuladaDto(
                Exitoso: false,
                PrecioFinal: 0,
                PrecioBase: 0,
                Recargo: 0,
                Bonificacion: 0,
                VersionCuadroAplicada: 0,
                ReglaAplicada: null,
                Mensaje: "No hay ningún cuadro tarifario vigente disponible para calcular precios."
            );
        }

        var regla = vigente.ObtenerReglaAplicable(
            solicitud.PesoKg,
            solicitud.VolumenM3,
            solicitud.ModalidadServicio,
            solicitud.ZonaCoberturaId);

        if (regla == null)
        {
            return new CotizacionSimuladaDto(
                Exitoso: false,
                PrecioFinal: 0,
                PrecioBase: 0,
                Recargo: 0,
                Bonificacion: 0,
                VersionCuadroAplicada: vigente.Version,
                ReglaAplicada: null,
                Mensaje: "Ninguna regla de la versión vigente cubre ese peso, volumen y modalidad de servicio."
            );
        }

        if (mapaNombresZonas is null)
        {
            var zonasDelOperador = await _zonaRepositorio.ListarZonasDelOperadorAsync(operadorId, null, null, cancellationToken);
            mapaNombresZonas = zonasDelOperador.ToDictionary(z => z.Id, z => z.Nombre);
        }

        var ambito = regla.ZonaCoberturaId.HasValue && mapaNombresZonas.TryGetValue(regla.ZonaCoberturaId.Value, out var nombreZona)
            ? nombreZona
            : "Todas las zonas";

        return new CotizacionSimuladaDto(
            Exitoso: true,
            PrecioFinal: regla.CalcularPrecioFinal(),
            PrecioBase: regla.PrecioBase,
            Recargo: regla.Recargo,
            Bonificacion: regla.Bonificacion,
            VersionCuadroAplicada: vigente.Version,
            ReglaAplicada: $"{regla.ModalidadServicio} ({ambito}) — Rango {regla.PesoDesde}kg a {regla.PesoHasta}kg",
            Mensaje: "Tarifa calculada con éxito bajo la versión vigente."
        );
    }

    private static CuadroTarifario ReconstruirDominioDesdeCache(CuadroTarifarioDetalleDto dto)
    {
        var cuadro = new CuadroTarifario(dto.Id, dto.OperadorId, dto.Version, dto.VigenteDesde, dto.VigenteHasta);
        cuadro.RegistrarComentario(dto.Comentario);
        foreach (var r in dto.ReglasTarifa)
        {
            cuadro.AgregarRegla(new ReglaTarifa(
                r.Id,
                dto.Id,
                r.ModalidadServicio,
                r.PesoDesde,
                r.PesoHasta,
                r.VolumenDesde,
                r.VolumenHasta,
                r.PrecioBase,
                r.Recargo,
                r.Bonificacion,
                r.ZonaCoberturaId));
        }

        return cuadro;
    }
}
