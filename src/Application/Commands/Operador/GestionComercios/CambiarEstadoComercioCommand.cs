using MediatR;

namespace UltimaMilla.Application.Commands.Operador.GestionComercios;

/// <summary>
/// Comando para activar o suspender la relación de un comercio con el operador (baja lógica).
/// </summary>
public record CambiarEstadoComercioCommand(
    Guid Id,
    bool Activo
) : IRequest;

