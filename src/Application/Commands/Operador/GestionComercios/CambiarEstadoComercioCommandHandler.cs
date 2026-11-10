using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Operador.GestionComercios;

/// <summary>
/// Manejador para cambiar el estado activo/suspendido de un comercio con el operador.
/// </summary>
public class CambiarEstadoComercioCommandHandler : IRequestHandler<CambiarEstadoComercioCommand>
{
    private readonly IComercioOperadorRepository _comercioOperadorRepositorio;
    private readonly ICurrentTenant _inquilinoActual;
    private readonly IUnitOfWork _unidadDeTrabajo;
    private readonly ITableroRealtimeNotifier? _tiempoReal;

    public CambiarEstadoComercioCommandHandler(
        IComercioOperadorRepository comercioOperadorRepositorio,
        ICurrentTenant inquilinoActual,
        IUnitOfWork unidadDeTrabajo,
        ITableroRealtimeNotifier? tiempoReal = null)
    {
        _tiempoReal = tiempoReal;
        _comercioOperadorRepositorio = comercioOperadorRepositorio;
        _inquilinoActual = inquilinoActual;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task Handle(CambiarEstadoComercioCommand solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var operadorId = _inquilinoActual.OperadorId;
        if (operadorId == Guid.Empty)
            throw new UnauthorizedAccessException("Operador no autenticado.");

        if (solicitud.Id == Guid.Empty)
            throw new DomainValidationException("El identificador del comercio es inválido.");

        var vinculo = await _comercioOperadorRepositorio.ObtenerPorComercioYOperadorAsync(solicitud.Id, operadorId, cancellationToken);
        if (vinculo is null)
            throw new NotFoundException("No se encontró el comercio solicitado.");

        if (solicitud.Activo)
        {
            vinculo.Activar();
        }
        else
        {
            vinculo.Suspender();
        }

        await _comercioOperadorRepositorio.ActualizarAsync(vinculo, cancellationToken);
        await _unidadDeTrabajo.SaveChangesAsync(cancellationToken);

        // Las sesiones abiertas del comercio actualizan su lista de operadores sin recargar; un fallo del aviso no deshace el cambio.
        if (_tiempoReal is not null)
        {
            try
            {
                await _tiempoReal.NotificarOperadoresDelComercioAsync(vinculo.ComercioId, cancellationToken);
            }
            catch
            {
                // El comercio verá el cambio al recargar
            }
        }
    }
}

