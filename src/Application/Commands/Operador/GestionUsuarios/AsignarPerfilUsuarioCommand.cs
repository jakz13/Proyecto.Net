using MediatR;

namespace UltimaMilla.Application.Commands.Operador.GestionUsuarios;

/// <summary>
/// Comando para asignar o cambiar el perfil (rol) de un usuario puntual.
/// </summary>
public record AsignarPerfilUsuarioCommand(
    Guid Id,
    Guid PerfilId
) : IRequest;

