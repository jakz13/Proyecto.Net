using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Operador.GestionUsuarios;

/// <summary>
/// Manejador para activar o desactivar un usuario del operador (baja lógica).
/// </summary>
public class CambiarEstadoUsuarioCommandHandler : IRequestHandler<CambiarEstadoUsuarioCommand>
{
    private readonly IUsuarioRepository _usuarioRepositorio;
    private readonly ICurrentTenant _inquilinoActual;
    private readonly IUnitOfWork _unidadDeTrabajo;

    public CambiarEstadoUsuarioCommandHandler(
        IUsuarioRepository usuarioRepositorio,
        ICurrentTenant inquilinoActual,
        IUnitOfWork unidadDeTrabajo)
    {
        _usuarioRepositorio = usuarioRepositorio;
        _inquilinoActual = inquilinoActual;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task Handle(CambiarEstadoUsuarioCommand solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var operadorId = _inquilinoActual.OperadorId;
        if (operadorId == Guid.Empty)
            throw new UnauthorizedAccessException("Operador no autenticado.");

        if (solicitud.Id == Guid.Empty)
            throw new DomainValidationException("El identificador del usuario es inválido.");

        var usuario = await _usuarioRepositorio.ObtenerPorIdYOperadorAsync(solicitud.Id, operadorId, cancellationToken);
        if (usuario is null)
            throw new NotFoundException("No se encontró el usuario solicitado.");

        if (solicitud.Activo)
        {
            usuario.Activar();
        }
        else
        {
            usuario.Desactivar();
        }

        await _usuarioRepositorio.ActualizarAsync(usuario, cancellationToken);
        await _unidadDeTrabajo.SaveChangesAsync(cancellationToken);
    }
}

