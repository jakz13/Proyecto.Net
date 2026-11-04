using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Services;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Operador.GestionZonas;

/// <summary>
/// Manejador para definir una nueva zona de cobertura y asociar sus franjas horarias con validación de superposiciones.
/// </summary>
public class CrearZonaCoberturaCommandHandler : IRequestHandler<CrearZonaCoberturaCommand, ZonaCoberturaDetalleDto>
{
    private readonly IZonaCoberturaRepository _zonaRepositorio;
    private readonly ICurrentTenant _inquilinoActual;
    private readonly IUnitOfWork _unidadDeTrabajo;

    public CrearZonaCoberturaCommandHandler(
        IZonaCoberturaRepository zonaRepositorio,
        ICurrentTenant inquilinoActual,
        IUnitOfWork unidadDeTrabajo)
    {
        _zonaRepositorio = zonaRepositorio;
        _inquilinoActual = inquilinoActual;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<ZonaCoberturaDetalleDto> Handle(CrearZonaCoberturaCommand solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var operadorId = _inquilinoActual.OperadorId;
        if (operadorId == Guid.Empty)
            throw new UnauthorizedAccessException("Operador no autenticado.");

        var nombreNormalizado = (solicitud.Nombre ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nombreNormalizado))
            throw new DomainValidationException("El nombre de la zona de cobertura no puede ser vacío.");

        var poligonoNormalizado = (solicitud.PoligonoGeografico ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(poligonoNormalizado))
            throw new DomainValidationException("El polígono geográfico de la zona no puede ser vacío.");

        var franjas = solicitud.FranjasHorarias ?? Array.Empty<CrearFranjaHorariaDto>();
        ValidadorFranjasHorarias.Validar(franjas);

        var existeNombre = await _zonaRepositorio.ExisteNombreAsync(nombreNormalizado, operadorId, null, cancellationToken);
        if (existeNombre)
            throw new DomainValidationException("Ya existe una zona de cobertura con ese nombre.");

        var nuevaZona = new ZonaCobertura(Guid.NewGuid(), operadorId, nombreNormalizado, poligonoNormalizado);
        nuevaZona.ActualizarDatos(nombreNormalizado, solicitud.ColorHex);

        foreach (var franjaDto in franjas)
        {
            var franja = new FranjaHoraria(
                Guid.NewGuid(),
                nuevaZona.Id,
                franjaDto.DiaSemana,
                franjaDto.HoraInicio,
                franjaDto.HoraFin,
                franjaDto.CapacidadMaximaEnvios);

            nuevaZona.AgregarFranjaHoraria(franja);
        }

        await _zonaRepositorio.AgregarAsync(nuevaZona, cancellationToken);
        await _unidadDeTrabajo.SaveChangesAsync(cancellationToken);

        return Mapear(nuevaZona);
    }

    private static ZonaCoberturaDetalleDto Mapear(ZonaCobertura zona) => new(
        zona.Id,
        zona.OperadorId,
        zona.Nombre,
        zona.PoligonoGeografico,
        zona.ColorHex,
        zona.Activa,
        zona.FechaCreacion,
        zona.FranjasHorarias.Select(f => new FranjaHorariaDetalleDto(
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

