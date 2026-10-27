using MediatR;

namespace UltimaMilla.Application.Commands.Mensajeria.RegistrarMensajeFallido;

/// <summary>Deja en la cola de fallidos un mensaje cuyos reintentos automáticos se agotaron (§6.8), visible para UC-42.</summary>
public record RegistrarMensajeFallidoCommand(
    Guid MessageId,
    string AgregadoOrigen,
    Guid AgregadoId,
    string TipoEvento,
    string Payload,
    string Excepcion,
    int Intentos,
    string ColaOrigen) : IRequest<Guid>;
