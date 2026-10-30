using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Commands.Operador.GestionUsuarios;

/// <summary>
/// Comando para modificar los datos y perfil de un usuario existente (UC-01, RF-01).
/// </summary>
public record ActualizarUsuarioCommand(
    Guid Id,
    string Nombre,
    string Email,
    Guid PerfilId,
    bool Activo
) : IRequest<UsuarioDetalleDto>;

