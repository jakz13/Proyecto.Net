using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Queries.Planificacion;

public class ListarEnviosPendientesDeRutaQueryHandler : IRequestHandler<ListarEnviosPendientesDeRutaQuery, IReadOnlyList<EnvioPendienteDeRutaDto>>
{
    private readonly IEnvioRepository _envioRepository;
    private readonly ICurrentTenant _currentTenant;

    public ListarEnviosPendientesDeRutaQueryHandler(IEnvioRepository envioRepository, ICurrentTenant currentTenant)
    {
        _envioRepository = envioRepository;
        _currentTenant = currentTenant;
    }

    public async Task<IReadOnlyList<EnvioPendienteDeRutaDto>> Handle(ListarEnviosPendientesDeRutaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var enDeposito = await _envioRepository.ListarPorEstadoAsync(_currentTenant.OperadorId, EstadoEnvio.EnDeposito, cancellationToken);
        var entregadosParcialmente = await _envioRepository.ListarPorEstadoAsync(_currentTenant.OperadorId, EstadoEnvio.EntregadoParcialmente, cancellationToken);

        // Solo se ofrecen los envíos que realmente se podrían asignar: clasificados, con dirección de entrega y bultos pendientes disponibles (RF-34)
        return enDeposito
            .Concat(entregadosParcialmente)
            .DistinctBy(e => e.Id)
            .Where(e => e.ObtenerMotivoNoAsignable() is null)
            .Select(e => new EnvioPendienteDeRutaDto(
                e.Id,
                e.CodigoSeguimientoPublico,
                e.NombreDestinatario,
                e.DireccionDestino,
                e.ZonaClasificadaId,
                e.PrioridadMaxima?.ToString(),
                e.ModalidadServicio,
                e.PesoTotal,
                e.VolumenTotal,
                e.FranjaHorariaComprometidaId,
                e.SelloModificacion))
            .ToList();
    }
}
