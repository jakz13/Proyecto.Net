using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Application.Queries.Operador.GestionZonas;

/// <summary>
/// Manejador para listar zonas de cobertura del operador autenticado con sus franjas horarias.
/// </summary>
public class ListarZonasCoberturaQueryHandler : IRequestHandler<ListarZonasCoberturaQuery, IReadOnlyList<ZonaCoberturaDetalleDto>>
{
    private readonly IZonaCoberturaRepository _zonaRepositorio;
    private readonly ICurrentTenant _inquilinoActual;

    public ListarZonasCoberturaQueryHandler(
        IZonaCoberturaRepository zonaRepositorio,
        ICurrentTenant inquilinoActual)
    {
        _zonaRepositorio = zonaRepositorio;
        _inquilinoActual = inquilinoActual;
    }

    public async Task<IReadOnlyList<ZonaCoberturaDetalleDto>> Handle(ListarZonasCoberturaQuery solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var operadorId = _inquilinoActual.OperadorId;
        if (operadorId == Guid.Empty)
            throw new UnauthorizedAccessException("Operador no autenticado.");

        var zonas = await _zonaRepositorio.ListarZonasDelOperadorAsync(
            operadorId,
            solicitud.Busqueda,
            solicitud.SoloActivas,
            cancellationToken);

        return zonas.Select(Mapear).ToList();
    }

    private static ZonaCoberturaDetalleDto Mapear(ZonaCobertura zona) => new(
        zona.Id,
        zona.OperadorId,
        zona.Nombre,
        zona.PoligonoGeografico,
        zona.ColorHex,
        zona.Activa,
        zona.FechaCreacion,
        zona.FranjasHorarias
            .OrderBy(f => f.DiaSemana == DayOfWeek.Sunday ? 7 : (int)f.DiaSemana)
            .ThenBy(f => f.HoraInicio)
            .Select(f => new FranjaHorariaDetalleDto(
                f.Id,
                f.ZonaCoberturaId,
                f.DiaSemana,
                f.HoraInicio,
                f.HoraFin,
                f.CapacidadMaximaEnvios,
                f.Activa
            )).ToList()
    );
}

