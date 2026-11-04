using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Application.Queries.Operador.GestionTarifas;

/// <summary>
/// Consulta para obtener la versión actualmente vigente del cuadro tarifario del operador (UC-03).
/// </summary>
public record ObtenerCuadroTarifarioVigenteQuery : IRequest<CuadroTarifarioDetalleDto?>;

public class ObtenerCuadroTarifarioVigenteQueryHandler : IRequestHandler<ObtenerCuadroTarifarioVigenteQuery, CuadroTarifarioDetalleDto?>
{
    private readonly ICuadroTarifarioRepository _cuadroRepositorio;
    private readonly IZonaCoberturaRepository _zonaRepositorio;
    private readonly ICurrentTenant _inquilinoActual;

    public ObtenerCuadroTarifarioVigenteQueryHandler(
        ICuadroTarifarioRepository cuadroRepositorio,
        IZonaCoberturaRepository zonaRepositorio,
        ICurrentTenant inquilinoActual)
    {
        _cuadroRepositorio = cuadroRepositorio;
        _zonaRepositorio = zonaRepositorio;
        _inquilinoActual = inquilinoActual;
    }

    public async Task<CuadroTarifarioDetalleDto?> Handle(ObtenerCuadroTarifarioVigenteQuery solicitud, CancellationToken cancellationToken)
    {
        if (_inquilinoActual.OperadorId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Operador no autenticado.");
        }

        var operadorId = _inquilinoActual.OperadorId;
        var vigente = await _cuadroRepositorio.ObtenerVigenteAsync(operadorId, DateTime.UtcNow, cancellationToken);
        if (vigente == null)
        {
            return null;
        }

        var zonas = await _zonaRepositorio.ListarZonasDelOperadorAsync(operadorId, null, null, cancellationToken);
        var mapaNombresZonas = zonas.ToDictionary(z => z.Id, z => z.Nombre);

        return new CuadroTarifarioDetalleDto(
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
    }
}
