using MediatR;

namespace UltimaMilla.Application.Commands.AltaEnvio;

public record AltaEnvioCommand(
    Guid ComercioId,
    string DireccionDestino,
    string NombreDestinatario
) : IRequest<Guid>;
