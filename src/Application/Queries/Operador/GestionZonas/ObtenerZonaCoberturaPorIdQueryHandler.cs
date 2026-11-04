using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Operador.GestionZonas;

/// <summary>
/// Manejador para obtener el detalle de una zona de cobertura con verificación Anti-IDOR.
/// </summary>
public class ObtenerZonaCoberturaPorIdQueryHandler : IRequestHandler<ObtenerZonaCoberturaPorIdQuery, ZonaCoberturaDetalleDto>
{
    private readonly IZonaCoberturaRepository _zonaRepositorio;
    private readonly ICurrentTenant _inquilinoActual;

    public ObtenerZonaCoberturaPorIdQueryHandler(
        IZonaCoberturaRepository zonaRepositorio,
        ICurrentTenant inquilinoActual)
    {
        _zonaRepositorio = zonaRepositorio;
        _inquilinoActual = inquilinoActual;
    }

    public async Task<ZonaCoberturaDetalleDto> Handle(ObtenerZonaCoberturaPorIdQuery solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var operadorId = _inquilinoActual.OperadorId;
        if (operadorId == Guid.Empty)
            throw new UnauthorizedAccessException("Operador no autenticado.");

        if (solicitud.Id == Guid.Empty)
            throw new DomainValidationException("El identificador de la zona es inválido.");

        var zona = await _zonaRepositorio.ObtenerPorIdAsync(solicitud.Id, operadorId, cancellationToken);
        if (zona is null)
            throw new NotFoundException("No se encontró la zona de cobertura solicitada.");

        return new ZonaCoberturaDetalleDto(
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
}

