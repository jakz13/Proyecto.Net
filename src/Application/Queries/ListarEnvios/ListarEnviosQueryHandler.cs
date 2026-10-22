using MediatR;
using UltimaMilla.Application.DTOs;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Application.Queries.ListarEnvios;

public class ListarEnviosQueryHandler : IRequestHandler<ListarEnviosQuery, List<EnvioDto>>
{
    private const int MaximoEnvios = 500;

    private readonly IEnvioRepository _envioRepository;
    private readonly ICurrentTenant _currentTenant;

    public ListarEnviosQueryHandler(IEnvioRepository envioRepository, ICurrentTenant currentTenant)
    {
        _envioRepository = envioRepository;
        _currentTenant = currentTenant;
    }

    public async Task<List<EnvioDto>> Handle(ListarEnviosQuery request, CancellationToken cancellationToken)
    {
        // Solo los envíos del operador actual; un usuario de comercio ve únicamente los de su comercio
        var envios = await _envioRepository.ListarRecientesPorOperadorAsync(_currentTenant.OperadorId, MaximoEnvios, cancellationToken);

        return envios
            .Where(e => !_currentTenant.ComercioOperadorId.HasValue || e.ComercioOperadorId == _currentTenant.ComercioOperadorId)
            .Select(e => new EnvioDto(
                e.Id,
                e.ComercioId,
                e.DireccionDestino,
                e.NombreDestinatario,
                e.FechaCreacion,
                e.Estado.ToString(),
                e.CodigoSeguimientoPublico))
            .ToList();
    }
}
