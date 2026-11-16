using MediatR;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Operador.GestionUsuarios;

/// <summary>
/// Manejador para la creación de un nuevo usuario en el operador o comercio asociado.
/// Concatena limpiamente Nombre y Apellido si son provistos por separado.
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

        if (PoliticaDePassword.Validar(solicitud.Password) is { } motivoPassword)
            throw new DomainValidationException(motivoPassword);

        if (solicitud.PerfilId == Guid.Empty)
            throw new DomainValidationException("Debe seleccionar un perfil válido.");

        // Validar existencia del perfil primero para determinar si es un Administrador de Comercio
        var perfil = await _usuarioRepositorio.ObtenerPerfilPorIdAsync(solicitud.PerfilId, cancellationToken);

        // Validar unicidad de correo (permitiendo vincular un usuario de comercio existente cuando otro operador lo asocia)
        var existeEmail = await _usuarioRepositorio.ExisteEmailAsync(emailNormalizado, null, cancellationToken);
        if (existeEmail)
        {
            if (perfil is { EsAdministradorDeComercio: true } && solicitud.ComercioId is { } idComercioSolicitado && idComercioSolicitado != Guid.Empty)
            {
                var usuarioExistente = await _usuarioRepositorio.ObtenerPorEmailAsync(emailNormalizado, cancellationToken);
                if (usuarioExistente is { ComercioId: { } idComercioExistente } && usuarioExistente.OperadorId != operadorId)
                {
                    var perteneceSolicitado = await _comercioOperadorRepositorio.ExisteRelacionAsync(idComercioSolicitado, operadorId, cancellationToken);
                    if (!perteneceSolicitado)
                        throw new DomainValidationException("El comercio seleccionado no pertenece al operador.");

                    var yaVinculado = await _comercioOperadorRepositorio.ExisteRelacionAsync(idComercioExistente, operadorId, cancellationToken);
                    if (!yaVinculado)
                    {
                        var claveAleatoria = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
                        var huellaClave = UltimaMilla.Domain.Entities.Comercio.ComercioOperador.CalcularHuellaClaveApi(claveAleatoria);
                        await _comercioOperadorRepositorio.AgregarAsync(
                            new UltimaMilla.Domain.Entities.Comercio.ComercioOperador(Guid.NewGuid(), idComercioExistente, operadorId, huellaClave, "Activo"),
                            cancellationToken);
                        await _unidadDeTrabajo.SaveChangesAsync(cancellationToken);
                    }

                    var comercioVinculado = await _comercioRepositorio.ObtenerPorIdAsync(idComercioSolicitado, cancellationToken);
                    var nombreComercioVinculado = comercioVinculado is null
                        ? null
                        : (string.IsNullOrWhiteSpace(comercioVinculado.NombreFantasia) ? comercioVinculado.RazonSocial : comercioVinculado.NombreFantasia);

                    return new UsuarioDetalleDto(
                        usuarioExistente.Id,
                        usuarioExistente.Nombre,
                        usuarioExistente.Email,
                        usuarioExistente.PerfilId,
                        perfil.Nombre,
                        operadorId,
                        idComercioExistente,
                        nombreComercioVinculado,
                        usuarioExistente.Activo,
                        usuarioExistente.FechaAlta);
                }
            }

            throw new DomainValidationException("Ya existe un usuario registrado con ese correo electrónico.");
        }

        if (perfil is null)
            throw new DomainValidationException("El perfil seleccionado no existe.");

        if (perfil.EsDePlataforma)
            throw new DomainValidationException("El perfil de plataforma no se asigna desde un operador.");

        // Solo el administrador de comercio se asocia a un comercio: un repartidor, despachador u operario son personal del operador
        var esAdministradorDeComercio = perfil.EsAdministradorDeComercio;
        var traeComercio = solicitud.ComercioId.HasValue && solicitud.ComercioId.Value != Guid.Empty;
        if (traeComercio && !esAdministradorDeComercio)
            throw new DomainValidationException("Solo un usuario con perfil Administrador de comercio se asocia a un comercio.");
        if (esAdministradorDeComercio && !traeComercio)
            throw new DomainValidationException("Seleccione el comercio asociado al administrador de comercio.");

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

        if (!string.IsNullOrWhiteSpace(solicitud.Documento))
            UltimaMilla.Domain.Common.Cedula.Validar(solicitud.Documento);

        var nombreCompleto = string.IsNullOrWhiteSpace(solicitud.Apellido)
            ? (solicitud.Nombre ?? string.Empty).Trim()
            : $"{(solicitud.Nombre ?? string.Empty).Trim()} {solicitud.Apellido.Trim()}";

        var hashContrasena = _hasheadorContrasena.HashearPassword(solicitud.Password);
        var nuevoUsuario = new Usuario(
            Guid.NewGuid(),
            nombreCompleto,
            emailNormalizado,
            hashContrasena,
            solicitud.PerfilId,
            operadorId,
            solicitud.ComercioId
        );

        await _usuarioRepositorio.AgregarAsync(nuevoUsuario, cancellationToken);
        await _usuarioRepositorio.SincronizarRepartidorDeUsuarioAsync(
            nuevoUsuario,
            perfil.Nombre,
            solicitud.Documento,
            solicitud.Telefono,
            solicitud.VehiculoAsignadoId,
            cancellationToken);
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
