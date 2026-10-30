using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Operador.GestionUsuarios;

/// <summary>
/// Manejador para obtener el detalle de un usuario validando la pertenencia al operador (Anti-IDOR).
/// </summary>
public class ObtenerUsuarioPorIdQueryHandler : IRequestHandler<ObtenerUsuarioPorIdQuery, UsuarioDetalleDto>
{
    private readonly IUsuarioRepository _usuarioRepositorio;
    private readonly IComercioRepository _comercioRepositorio;
    private readonly ICurrentTenant _inquilinoActual;

    public ObtenerUsuarioPorIdQueryHandler(
        IUsuarioRepository usuarioRepositorio,
        IComercioRepository comercioRepositorio,
        ICurrentTenant inquilinoActual)
    {
        _usuarioRepositorio = usuarioRepositorio;
        _comercioRepositorio = comercioRepositorio;
        _inquilinoActual = inquilinoActual;
    }

    public async Task<UsuarioDetalleDto> Handle(ObtenerUsuarioPorIdQuery solicitud, CancellationToken cancellationToken)
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

        var perfil = await _usuarioRepositorio.ObtenerPerfilPorIdAsync(usuario.PerfilId, cancellationToken);
        var nombrePerfil = perfil?.Nombre ?? string.Empty;

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
            nombrePerfil,
            usuario.OperadorId,
            usuario.ComercioId,
            nombreComercio,
            usuario.Activo,
            usuario.FechaAlta
        );
    }
}

