using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Queries.Deposito.ListarEnviosAdmitidos;

public class ListarEnviosAdmitidosQueryHandler : IRequestHandler<ListarEnviosAdmitidosQuery, IReadOnlyList<EnvioAdmitidoDto>>
{
    private readonly IEnvioRepository _envioRepository;
    private readonly ICurrentTenant _currentTenant;

    public ListarEnviosAdmitidosQueryHandler(IEnvioRepository envioRepository, ICurrentTenant currentTenant)
    {
        _envioRepository = envioRepository ?? throw new ArgumentNullException(nameof(envioRepository));
        _currentTenant = currentTenant ?? throw new ArgumentNullException(nameof(currentTenant));
    }

    public async Task<IReadOnlyList<EnvioAdmitidoDto>> Handle(ListarEnviosAdmitidosQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var operadorId = _currentTenant.OperadorId;
        var enviosAdmitidos = await _envioRepository.ListarPorEstadoAsync(operadorId, EstadoEnvio.Admitido, cancellationToken);

        return enviosAdmitidos
            .Select(envio => new EnvioAdmitidoDto(
                envio.Id,
                envio.CodigoSeguimientoPublico,
                envio.NombreDestinatario,
                envio.DireccionDestino,
                null,
                envio.ModalidadServicio,
                envio.PesoTotal,
                envio.Bultos.Count,
                envio.FechaAlta,
                envio.Bultos.Select(bulto => new BultoResumenDto(
                    bulto.Id,
                    bulto.Identificacion,
                    bulto.Peso,
                    bulto.Alto,
                    bulto.Ancho,
                    bulto.Profundo,
                    bulto.EstadoBulto)).ToList()
            ))
            .ToList();
    }
}
