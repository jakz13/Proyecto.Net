using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;

namespace UltimaMilla.Application.Commands.AltaEnvio;

public class AltaEnvioCommandHandler : IRequestHandler<AltaEnvioCommand, Guid>
{
    private readonly IEnvioRepository _envioRepository;

    public AltaEnvioCommandHandler(IEnvioRepository envioRepository)
    {
        _envioRepository = envioRepository;
    }

    public async Task<Guid> Handle(AltaEnvioCommand request, CancellationToken cancellationToken)
    {
        var envio = new Envio(
            Guid.NewGuid(),
            request.ComercioId,
            request.DireccionDestino,
            request.NombreDestinatario
        );

        await _envioRepository.AddAsync(envio, cancellationToken);

        return envio.Id;
    }
}
