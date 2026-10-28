using MediatR;

namespace UltimaMilla.Application.Commands.Tablero.EnviarInstruccionRepartidor;

/// <summary>
/// Envía una instrucción operativa o directiva urgente al repartidor en su terminal móvil (UC-12, RF-29).
/// </summary>
public record EnviarInstruccionRepartidorCommand(Guid RepartidorId, string Mensaje) : IRequest<bool>;

public record EnviarInstruccionRequest(Guid RepartidorId, string Mensaje);
