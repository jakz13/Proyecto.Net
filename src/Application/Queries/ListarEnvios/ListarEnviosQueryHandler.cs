using MediatR;
using UltimaMilla.Application.DTOs;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Application.Queries.ListarEnvios;

public class ListarEnviosQueryHandler : IRequestHandler<ListarEnviosQuery, List<EnvioDto>>
{
    private readonly IEnvioRepository _envioRepository;

    public ListarEnviosQueryHandler(IEnvioRepository envioRepository)
    {
        _envioRepository = envioRepository;
    }

    public async Task<List<EnvioDto>> Handle(ListarEnviosQuery request, CancellationToken cancellationToken)
    {
        var envios = await _envioRepository.GetAllAsync(cancellationToken);

        return envios.Select(e => new EnvioDto(
            e.Id,
            e.ComercioId,
            e.DireccionDestino,
            e.NombreDestinatario,
            e.FechaCreacion,
            e.Estado.ToString()
        )).ToList();
    }
}
