using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Queries.Deposito.ListarEnviosEnDeposito;

public class ListarEnviosEnDepositoQueryHandler : IRequestHandler<ListarEnviosEnDepositoQuery, IReadOnlyList<EnvioEnDepositoDto>>
{
    private readonly IEnvioRepository _envioRepository;
    private readonly ICurrentTenant _currentTenant;

    public ListarEnviosEnDepositoQueryHandler(IEnvioRepository envioRepository, ICurrentTenant currentTenant)
    {
        _envioRepository = envioRepository ?? throw new ArgumentNullException(nameof(envioRepository));
        _currentTenant = currentTenant ?? throw new ArgumentNullException(nameof(currentTenant));
    }

    public async Task<IReadOnlyList<EnvioEnDepositoDto>> Handle(ListarEnviosEnDepositoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var operadorId = _currentTenant.OperadorId;
        var enviosEnDeposito = await _envioRepository.ListarPorEstadoAsync(operadorId, EstadoEnvio.EnDeposito, cancellationToken);

        return enviosEnDeposito
            .Select(envio =>
            {
                var bultosRecibidosODanados = envio.Bultos
                    .Where(b => b.EstadoBulto is "Recibido" or "Danado")
                    .ToList();

                var clasificados = bultosRecibidosODanados.Count(b => b.EstaClasificado);

                return new EnvioEnDepositoDto(
                    envio.Id,
                    envio.CodigoSeguimientoPublico,
                    envio.NombreDestinatario,
                    envio.DireccionDestino,
                    envio.ModalidadServicio,
                    envio.PesoTotal,
                    bultosRecibidosODanados.Count,
                    clasificados,
                    envio.EstaClasificado,
                    envio.FechaAlta,
                    bultosRecibidosODanados.Select(b => new BultoClasificadoResumenDto(
                        b.Id,
                        b.Identificacion,
                        b.Peso,
                        b.EstadoBulto,
                        b.EstaClasificado,
                        b.Prioridad?.ToString(),
                        b.ZonaClasificadaId,
                        b.TipoClasificacion,
                        b.FechaClasificacion)).ToList()
                );
            })
            .ToList();
    }
}
