using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Application.Queries.Operador.GestionTarifas;

/// <summary>
/// Consulta para listar el historial completo de versiones de cuadros tarifarios del operador (UC-03).
/// </summary>
public record ListarVersionesCuadroTarifarioQuery : IRequest<IReadOnlyList<CuadroTarifarioDetalleDto>>;

public class ListarVersionesCuadroTarifarioQueryHandler : IRequestHandler<ListarVersionesCuadroTarifarioQuery, IReadOnlyList<CuadroTarifarioDetalleDto>>
{
    private readonly ICuadroTarifarioRepository _cuadroRepositorio;
    private readonly IZonaCoberturaRepository _zonaRepositorio;
    private readonly ICurrentTenant _inquilinoActual;

    public ListarVersionesCuadroTarifarioQueryHandler(
        ICuadroTarifarioRepository cuadroRepositorio,
        IZonaCoberturaRepository zonaRepositorio,
        ICurrentTenant inquilinoActual)
    {
        _cuadroRepositorio = cuadroRepositorio;
        _zonaRepositorio = zonaRepositorio;
        _inquilinoActual = inquilinoActual;
    }

    public async Task<IReadOnlyList<CuadroTarifarioDetalleDto>> Handle(ListarVersionesCuadroTarifarioQuery solicitud, CancellationToken cancellationToken)
    {
        if (_inquilinoActual.OperadorId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Operador no autenticado.");
        }

        var operadorId = _inquilinoActual.OperadorId;
        var cuadros = await _cuadroRepositorio.ListarVersionesAsync(operadorId, cancellationToken);
        var zonas = await _zonaRepositorio.ListarZonasDelOperadorAsync(operadorId, null, null, cancellationToken);
        var mapaNombresZonas = zonas.ToDictionary(z => z.Id, z => z.Nombre);
        var ahoraUtc = DateTime.UtcNow;

        return cuadros.Select(c => new CuadroTarifarioDetalleDto(
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
    }
}
