using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Queries.Deposito.ListarEnviosEnDeposito;

public class ListarEnviosEnDepositoQueryHandler : IRequestHandler<ListarEnviosEnDepositoQuery, IReadOnlyList<EnvioEnDepositoDto>>
{
    private readonly IEnvioRepository _envioRepository;
    private readonly ICurrentTenant _currentTenant;

    private readonly IDireccionRepository? _direccionRepository;

    public ListarEnviosEnDepositoQueryHandler(
        IEnvioRepository envioRepository,
        ICurrentTenant currentTenant,
        IDireccionRepository? direccionRepository = null)
    {
        _envioRepository = envioRepository ?? throw new ArgumentNullException(nameof(envioRepository));
        _currentTenant = currentTenant ?? throw new ArgumentNullException(nameof(currentTenant));
        _direccionRepository = direccionRepository;
    }

    public async Task<IReadOnlyList<EnvioEnDepositoDto>> Handle(ListarEnviosEnDepositoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var operadorId = _currentTenant.OperadorId;
        var enviosEnDeposito = await _envioRepository.ListarPorEstadoAsync(operadorId, EstadoEnvio.EnDeposito, cancellationToken);

        // Zona de la dirección de entrega: el depósito la propone por defecto al clasificar (el operario puede cambiarla)
        var zonaPorDireccion = new Dictionary<Guid, Guid?>();
        if (_direccionRepository is not null)
        {
            var ids = enviosEnDeposito.Where(e => e.DireccionEntregaId.HasValue).Select(e => e.DireccionEntregaId!.Value).Distinct().ToList();
            if (ids.Count > 0)
            {
                var direcciones = await _direccionRepository.GetByIdsAsync(ids, cancellationToken);
                zonaPorDireccion = direcciones.ToDictionary(d => d.Id, d => (Guid?)d.ZonaCoberturaId);
            }
        }

        return enviosEnDeposito
            .Select(envio =>
            {
                envio.NormalizarBultosEnDeposito();

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
                        b.FechaClasificacion)).ToList(),
                    envio.DireccionEntregaId.HasValue && zonaPorDireccion.TryGetValue(envio.DireccionEntregaId.Value, out var zona) ? zona : null
                );
            })
            .ToList();
    }
}
