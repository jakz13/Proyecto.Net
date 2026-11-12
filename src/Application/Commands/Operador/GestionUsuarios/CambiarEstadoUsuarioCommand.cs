using MediatR;

namespace UltimaMilla.Application.Commands.Operador.GestionUsuarios;

/// <summary>
/// Comando para dar de alta o baja lógica a un usuario del operador.
/// </summary>
public record CambiarEstadoUsuarioCommand(
    Guid Id,
    bool Activo
) : IRequest;

