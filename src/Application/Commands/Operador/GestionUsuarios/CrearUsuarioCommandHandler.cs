using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Operador.GestionUsuarios;

/// <summary>
/// Manejador para la creación de un nuevo usuario en el operador o comercio asociado.
/// </summary>
public class CrearUsuarioCommandHandler : IRequestHandler<CrearUsuarioCommand, UsuarioDetalleDto>
{
    private readonly IUsuarioRepository _usuarioRepositorio;
    private readonly IComercioRepository _comercioRepositorio;
    private readonly IComercioOperadorRepository _comercioOperadorRepositorio;
    private readonly IPasswordHasherService _hasheadorContrasena;
    private readonly ICurrentTenant _inquilinoActual;
    private readonly IUnitOfWork _unidadDeTrabajo;

    public CrearUsuarioCommandHandler(
        IUsuarioRepository usuarioRepositorio,
        IComercioRepository comercioRepositorio,
        IComercioOperadorRepository comercioOperadorRepositorio,
        IPasswordHasherService hasheadorContrasena,
        ICurrentTenant inquilinoActual,
        IUnitOfWork unidadDeTrabajo)
    {
        _usuarioRepositorio = usuarioRepositorio;
        _comercioRepositorio = comercioRepositorio;
        _comercioOperadorRepositorio = comercioOperadorRepositorio;
        _hasheadorContrasena = hasheadorContrasena;
        _inquilinoActual = inquilinoActual;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<UsuarioDetalleDto> Handle(CrearUsuarioCommand solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var operadorId = _inquilinoActual.OperadorId;
        if (operadorId == Guid.Empty)
            throw new UnauthorizedAccessException("Operador no autenticado.");

        var emailNormalizado = (solicitud.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(emailNormalizado))
            throw new DomainValidationException("El correo electrónico del usuario no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(solicitud.Nombre))
            throw new DomainValidationException("El nombre del usuario no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(solicitud.Password))
            throw new DomainValidationException("La contraseña del usuario no puede ser vacía.");

        if (solicitud.PerfilId == Guid.Empty)
            throw new DomainValidationException("Debe seleccionar un perfil válido.");

        // Validar unicidad de correo
        var existeEmail = await _usuarioRepositorio.ExisteEmailAsync(emailNormalizado, null, cancellationToken);
        if (existeEmail)
            throw new DomainValidationException("Ya existe un usuario registrado con ese correo electrónico.");

        // Validar existencia del perfil
        var perfil = await _usuarioRepositorio.ObtenerPerfilPorIdAsync(solicitud.PerfilId, cancellationToken);
        if (perfil is null)
            throw new DomainValidationException("El perfil seleccionado no existe.");

        string? nombreComercio = null;
        if (solicitud.ComercioId.HasValue && solicitud.ComercioId.Value != Guid.Empty)
        {
            var pertenece = await _comercioOperadorRepositorio.ExisteRelacionAsync(solicitud.ComercioId.Value, operadorId, cancellationToken);
            if (!pertenece)
                throw new DomainValidationException("El comercio seleccionado no pertenece al operador.");

            var comercio = await _comercioRepositorio.ObtenerPorIdAsync(solicitud.ComercioId.Value, cancellationToken);
            if (comercio is not null)
            {
                nombreComercio = string.IsNullOrWhiteSpace(comercio.NombreFantasia) ? comercio.RazonSocial : comercio.NombreFantasia;
            }
        }

        var hashContrasena = _hasheadorContrasena.HashearPassword(solicitud.Password);
        var nuevoUsuario = new Usuario(
            Guid.NewGuid(),
            solicitud.Nombre,
            emailNormalizado,
            hashContrasena,
            solicitud.PerfilId,
            operadorId,
            solicitud.ComercioId
        );

        await _usuarioRepositorio.AgregarAsync(nuevoUsuario, cancellationToken);
        await _unidadDeTrabajo.SaveChangesAsync(cancellationToken);

        return new UsuarioDetalleDto(
            nuevoUsuario.Id,
            nuevoUsuario.Nombre,
            nuevoUsuario.Email,
            nuevoUsuario.PerfilId,
            perfil.Nombre,
            nuevoUsuario.OperadorId,
            nuevoUsuario.ComercioId,
            nombreComercio,
            nuevoUsuario.Activo,
            nuevoUsuario.FechaAlta
        );
    }
}

