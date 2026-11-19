using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Operador.GestionUsuarios;

/// <summary>
/// Manejador para asignar un perfil a un usuario con validación de existencia de perfil y Anti-IDOR.
/// </summary>
public class AsignarPerfilUsuarioCommandHandler : IRequestHandler<AsignarPerfilUsuarioCommand>
{
    private readonly IUsuarioRepository _usuarioRepositorio;
    private readonly ICurrentTenant _inquilinoActual;
    private readonly IUnitOfWork _unidadDeTrabajo;

    private readonly ISesionRealtimeNotifier? _sesionNotifier;

    public AsignarPerfilUsuarioCommandHandler(
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

    public async Task Handle(AsignarPerfilUsuarioCommand solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var operadorId = _inquilinoActual.OperadorId;
        if (operadorId == Guid.Empty)
            throw new UnauthorizedAccessException("Operador no autenticado.");

        if (solicitud.Id == Guid.Empty)
            throw new DomainValidationException("El identificador del usuario es inválido.");

        if (solicitud.PerfilId == Guid.Empty)
            throw new DomainValidationException("Debe indicar un perfil válido.");

        var usuario = await _usuarioRepositorio.ObtenerPorIdYOperadorAsync(solicitud.Id, operadorId, cancellationToken);
        if (usuario is null)
            throw new NotFoundException("No se encontró el usuario solicitado.");

        var perfil = await _usuarioRepositorio.ObtenerPerfilPorIdAsync(solicitud.PerfilId, cancellationToken);
        if (perfil is null)
            throw new DomainValidationException("El perfil seleccionado no existe.");

        if (perfil.EsDePlataforma)
            throw new DomainValidationException("El perfil de plataforma no se asigna desde un operador.");

        usuario.AsignarPerfil(solicitud.PerfilId);

        await _usuarioRepositorio.ActualizarAsync(usuario, cancellationToken);
        await _usuarioRepositorio.SincronizarRepartidorDeUsuarioAsync(
            usuario,
            perfil.Nombre,
            cancellationToken: cancellationToken);
        await _unidadDeTrabajo.SaveChangesAsync(cancellationToken);

        // Con otro perfil cambian sus permisos y su menú: la sesión abierta se cierra para que vuelva a entrar con el perfil nuevo
        if (_sesionNotifier is not null)
        {
            try
            {
                await _sesionNotifier.NotificarSesionRevocadaAsync(usuario.Id, "El administrador cambió su perfil. Vuelva a iniciar sesión.", cancellationToken);
            }
            catch
            {
                // El fallo de transporte no deshace el cambio ya guardado
            }
        }
    }
}

