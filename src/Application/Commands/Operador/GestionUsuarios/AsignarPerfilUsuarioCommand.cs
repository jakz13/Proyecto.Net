using MediatR;

namespace UltimaMilla.Application.Commands.Operador.GestionUsuarios;

/// <summary>
/// Comando para asignar o cambiar el perfil (rol) de un usuario puntual (UC-01, RF-01).
/// </summary>
public record AsignarPerfilUsuarioCommand(
    Guid Id,
    Guid PerfilId
) : IRequest;

