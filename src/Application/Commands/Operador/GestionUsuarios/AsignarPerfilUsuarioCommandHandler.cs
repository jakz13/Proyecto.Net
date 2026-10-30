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

    public AsignarPerfilUsuarioCommandHandler(
        IUsuarioRepository usuarioRepositorio,
        ICurrentTenant inquilinoActual,
        IUnitOfWork unidadDeTrabajo)
    {
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

        usuario.AsignarPerfil(solicitud.PerfilId);

        await _usuarioRepositorio.ActualizarAsync(usuario, cancellationToken);
        await _unidadDeTrabajo.SaveChangesAsync(cancellationToken);
    }
}

