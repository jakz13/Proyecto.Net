using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Queries.Deposito;

/// <summary>
/// Manejador de la consulta para listar hojas de ruta pendientes de rendición en depósito (UC-16).
/// Considera las rutas que hayan sido despachadas, se encuentren en tránsito o hayan sido finalizadas en calle.
/// </summary>
public class ListarRutasPendientesRendicionQueryHandler : IRequestHandler<ListarRutasPendientesRendicionQuery, IReadOnlyList<RutaPendienteRendicionResumenDto>>
{
    private static readonly string[] EstadosPendientesRendicion = { "Despachada", "EnTransito", "Finalizada" };

    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IVehiculoRepository _vehiculoRepository;
    private readonly IRepartidorRepository _repartidorRepository;
    private readonly ICurrentTenant _currentTenant;

    public ListarRutasPendientesRendicionQueryHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IVehiculoRepository vehiculoRepository,
        IRepartidorRepository repartidorRepository,
        ICurrentTenant currentTenant)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _vehiculoRepository = vehiculoRepository;
        _repartidorRepository = repartidorRepository;
        _currentTenant = currentTenant;
    }

    public async Task<IReadOnlyList<RutaPendienteRendicionResumenDto>> Handle(ListarRutasPendientesRendicionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var operadorId = _currentTenant.OperadorId;
        var rutas = await _rutaRepository.ListarAsync(
            operadorId,
            fecha: null,
            repartidorId: null,
            estados: EstadosPendientesRendicion,
            cancellationToken);

        if (rutas.Count == 0)
        {
            return Array.Empty<RutaPendienteRendicionResumenDto>();
        }

        var repartidoresIds = rutas.Where(r => r.RepartidorId.HasValue).Select(r => r.RepartidorId!.Value).Distinct();
        var vehiculosIds = rutas.Where(r => r.VehiculoId.HasValue).Select(r => r.VehiculoId!.Value).Distinct();
        var enviosIds = rutas.SelectMany(r => r.Paradas).Select(p => p.EnvioId).Distinct();

        var repartidores = (await _repartidorRepository.GetByIdsAsync(repartidoresIds, cancellationToken))
            .ToDictionary(r => r.Id);

        var vehiculos = (await _vehiculoRepository.GetByIdsAsync(vehiculosIds, cancellationToken))
            .ToDictionary(v => v.Id);

        var envios = (await _envioRepository.GetByIdsAsync(enviosIds, cancellationToken))
            .Where(e => e.OperadorId == operadorId)
            .ToDictionary(e => e.Id);

        var resultado = new List<RutaPendienteRendicionResumenDto>();

        foreach (var ruta in rutas)
        {
            var paradasVigentes = ruta.Paradas.Where(p => p.Estado != "Cancelada").ToList();
            var enviosRuta = paradasVigentes
                .Where(p => envios.ContainsKey(p.EnvioId))
                .Select(p => envios[p.EnvioId])
                .DistinctBy(e => e.Id)
                .ToList();

            var bultosViajaron = enviosRuta
                .SelectMany(e => e.Bultos.Where(b => b.EstadoBulto is "CargadoEnVehiculo" or "EnRetornoADeposito"))
                .ToList();

            var bultosEntregados = enviosRuta
                .Where(e => e.EstadoActual == EstadoEnvio.Entregado)
                .SelectMany(e => e.Bultos.Where(b => b.EstadoBulto is "CargadoEnVehiculo" or "EnRetornoADeposito"))
                .Count();

            var bultosRetornoEstimados = bultosViajaron.Count - bultosEntregados;

            repartidores.TryGetValue(ruta.RepartidorId ?? Guid.Empty, out var repartidor);
            vehiculos.TryGetValue(ruta.VehiculoId ?? Guid.Empty, out var vehiculo);

            var numeroRuta = $"HR-{ruta.Fecha:yyyyMMdd}-{ruta.Id.ToString()[..4].ToUpperInvariant()}";

            resultado.Add(new RutaPendienteRendicionResumenDto(
                ruta.Id,
                numeroRuta,
                ruta.Fecha,
                ruta.Estado,
                ruta.RepartidorId,
                repartidor?.Documento,
                ruta.VehiculoId,
                vehiculo?.Patente,
                vehiculo?.Tipo,
                paradasVigentes.Count,
                bultosViajaron.Count,
                bultosEntregados,
                Math.Max(0, bultosRetornoEstimados),
                ruta.FechaDespacho));
        }

        return resultado.OrderByDescending(r => r.FechaDespacho ?? DateTime.MinValue).ToList();
    }
}
