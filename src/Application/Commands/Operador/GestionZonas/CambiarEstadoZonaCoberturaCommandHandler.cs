using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Operador.GestionZonas;

/// <summary>
/// Manejador para activar o desactivar una zona de cobertura del operador.
/// </summary>
public class CambiarEstadoZonaCoberturaCommandHandler : IRequestHandler<CambiarEstadoZonaCoberturaCommand>
{
    private readonly IZonaCoberturaRepository _zonaRepositorio;
    private readonly ICurrentTenant _inquilinoActual;
    private readonly IUnitOfWork _unidadDeTrabajo;

    public CambiarEstadoZonaCoberturaCommandHandler(
        IZonaCoberturaRepository zonaRepositorio,
        ICurrentTenant inquilinoActual,
        IUnitOfWork unidadDeTrabajo)
    {
        _zonaRepositorio = zonaRepositorio;
        _inquilinoActual = inquilinoActual;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task Handle(CambiarEstadoZonaCoberturaCommand solicitud, CancellationToken cancellationToken)
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

        if (solicitud.Activa)
            zona.Activar();
        else
            zona.Desactivar();

        await _zonaRepositorio.ActualizarAsync(zona, cancellationToken);
        await _unidadDeTrabajo.SaveChangesAsync(cancellationToken);
    }
}

