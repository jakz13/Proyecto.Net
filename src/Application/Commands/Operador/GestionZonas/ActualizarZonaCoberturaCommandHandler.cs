using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Services;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Operador.GestionZonas;

/// <summary>
/// Manejador para modificar los datos, polígono y franjas de una zona con verificación Anti-IDOR y validación de superposiciones.
/// </summary>
public class ActualizarZonaCoberturaCommandHandler : IRequestHandler<ActualizarZonaCoberturaCommand, ZonaCoberturaDetalleDto>
{
    private readonly IZonaCoberturaRepository _zonaRepositorio;
    private readonly ICurrentTenant _inquilinoActual;
    private readonly IUnitOfWork _unidadDeTrabajo;

    public ActualizarZonaCoberturaCommandHandler(
        IZonaCoberturaRepository zonaRepositorio,
        ICurrentTenant inquilinoActual,
        IUnitOfWork unidadDeTrabajo)
    {
        _zonaRepositorio = zonaRepositorio;
        _inquilinoActual = inquilinoActual;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<ZonaCoberturaDetalleDto> Handle(ActualizarZonaCoberturaCommand solicitud, CancellationToken cancellationToken)
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

        var nombreNormalizado = (solicitud.Nombre ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nombreNormalizado))
            throw new DomainValidationException("El nombre de la zona de cobertura no puede ser vacío.");

        var poligonoNormalizado = (solicitud.PoligonoGeografico ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(poligonoNormalizado))
            throw new DomainValidationException("El polígono geográfico de la zona no puede ser vacío.");

        var franjas = solicitud.FranjasHorarias ?? Array.Empty<CrearFranjaHorariaDto>();
        ValidadorFranjasHorarias.Validar(franjas);

        var existeNombre = await _zonaRepositorio.ExisteNombreAsync(nombreNormalizado, operadorId, solicitud.Id, cancellationToken);
        if (existeNombre)
            throw new DomainValidationException("Ya existe otra zona de cobertura con ese nombre.");

        zona.ActualizarDatos(nombreNormalizado, solicitud.ColorHex);
        zona.ActualizarPoligono(poligonoNormalizado);

        if (solicitud.Activa)
            zona.Activar();
        else
            zona.Desactivar();

        var nuevasFranjas = franjas.Select(f => new FranjaHoraria(
            Guid.NewGuid(),
            zona.Id,
            f.DiaSemana,
            f.HoraInicio,
            f.HoraFin,
            f.CapacidadMaximaEnvios
        )).ToList();

        await _zonaRepositorio.ReemplazarFranjasAsync(zona, nuevasFranjas, cancellationToken);
        await _zonaRepositorio.ActualizarAsync(zona, cancellationToken);
        await _unidadDeTrabajo.SaveChangesAsync(cancellationToken);

        return new ZonaCoberturaDetalleDto(
            zona.Id,
            zona.OperadorId,
            zona.Nombre,
            zona.PoligonoGeografico,
            zona.ColorHex,
            zona.Activa,
            zona.FechaCreacion,
            nuevasFranjas.Select(f => new FranjaHorariaDetalleDto(
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

