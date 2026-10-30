using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Commands.Operador.GestionUsuarios;

/// <summary>
/// Comando para dar de alta un usuario en el operador o asociado a un comercio (UC-01, RF-01).
/// </summary>
public record CrearUsuarioCommand(
    string Nombre,
    string Email,
    string Password,
    Guid PerfilId,
    Guid? ComercioId = null
) : IRequest<UsuarioDetalleDto>;

