using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Operador.GestionUsuarios;

/// <summary>
/// Manejador para modificar los datos, perfil y estado de un usuario del operador.
/// </summary>
public class ActualizarUsuarioCommandHandler : IRequestHandler<ActualizarUsuarioCommand, UsuarioDetalleDto>
{
    private readonly IUsuarioRepository _usuarioRepositorio;
    private readonly IComercioRepository _comercioRepositorio;
    private readonly ICurrentTenant _inquilinoActual;
    private readonly IUnitOfWork _unidadDeTrabajo;

    public ActualizarUsuarioCommandHandler(
        IUsuarioRepository usuarioRepositorio,
        IComercioRepository comercioRepositorio,
        ICurrentTenant inquilinoActual,
        IUnitOfWork unidadDeTrabajo)
    {
        _usuarioRepositorio = usuarioRepositorio;
        _comercioRepositorio = comercioRepositorio;
        _inquilinoActual = inquilinoActual;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<UsuarioDetalleDto> Handle(ActualizarUsuarioCommand solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var operadorId = _inquilinoActual.OperadorId;
        if (operadorId == Guid.Empty)
            throw new UnauthorizedAccessException("Operador no autenticado.");

        if (solicitud.Id == Guid.Empty)
            throw new DomainValidationException("El identificador del usuario es inválido.");

        var emailNormalizado = (solicitud.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(emailNormalizado))
            throw new DomainValidationException("El correo electrónico no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(solicitud.Nombre))
            throw new DomainValidationException("El nombre del usuario no puede ser vacío.");

        if (solicitud.PerfilId == Guid.Empty)
            throw new DomainValidationException("Debe seleccionar un perfil válido.");

        // Anti-IDOR: verificar que el usuario pertenezca al operador actual
        var usuario = await _usuarioRepositorio.ObtenerPorIdYOperadorAsync(solicitud.Id, operadorId, cancellationToken);
        if (usuario is null)
            throw new NotFoundException("No se encontró el usuario solicitado.");

        // Validar unicidad del correo electrónico
        var existeEmail = await _usuarioRepositorio.ExisteEmailAsync(emailNormalizado, solicitud.Id, cancellationToken);
        if (existeEmail)
            throw new DomainValidationException("Ya existe otro usuario con ese correo electrónico.");

        // Validar existencia del perfil
        var perfil = await _usuarioRepositorio.ObtenerPerfilPorIdAsync(solicitud.PerfilId, cancellationToken);
        if (perfil is null)
            throw new DomainValidationException("El perfil seleccionado no existe.");

        usuario.ActualizarDatos(solicitud.Nombre, emailNormalizado);
        usuario.AsignarPerfil(solicitud.PerfilId);

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

        string? nombreComercio = null;
        if (usuario.ComercioId.HasValue)
        {
            var comercio = await _comercioRepositorio.ObtenerPorIdAsync(usuario.ComercioId.Value, cancellationToken);
            if (comercio is not null)
            {
                nombreComercio = string.IsNullOrWhiteSpace(comercio.NombreFantasia) ? comercio.RazonSocial : comercio.NombreFantasia;
            }
        }

        return new UsuarioDetalleDto(
            usuario.Id,
            usuario.Nombre,
            usuario.Email,
            usuario.PerfilId,
            perfil.Nombre,
            usuario.OperadorId,
            usuario.ComercioId,
            nombreComercio,
            usuario.Activo,
            usuario.FechaAlta
        );
    }
}

