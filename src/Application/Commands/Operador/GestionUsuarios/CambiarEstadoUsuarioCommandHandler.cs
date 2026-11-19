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
    private readonly ISesionRealtimeNotifier? _sesionNotifier;

    public CambiarEstadoUsuarioCommandHandler(
        IUsuarioRepository usuarioRepositorio,
        ICurrentTenant inquilinoActual,
        IUnitOfWork unidadDeTrabajo,
        ISesionRealtimeNotifier? sesionNotifier = null)
    {
        _sesionNotifier = sesionNotifier;
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
        var perfil = await _usuarioRepositorio.ObtenerPerfilPorIdAsync(usuario.PerfilId, cancellationToken);
        if (perfil is not null)
        {
            await _usuarioRepositorio.SincronizarRepartidorDeUsuarioAsync(
                usuario,
                perfil.Nombre,
                cancellationToken: cancellationToken);
        }
        await _unidadDeTrabajo.SaveChangesAsync(cancellationToken);

        // Un usuario suspendido deja de operar en el momento, no recién cuando su próxima petición falle
        if (!solicitud.Activo && _sesionNotifier is not null)
        {
            try
            {
                await _sesionNotifier.NotificarSesionRevocadaAsync(usuario.Id, "Su cuenta fue suspendida por el administrador. Se cerró su sesión.", cancellationToken);
            }
            catch
            {
                // El fallo de transporte no deshace la suspensión ya guardada
            }
        }
    }
}

