using MediatR;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Application.Queries.Planificacion;

public class ListarHojasDeRutaQueryHandler : IRequestHandler<ListarHojasDeRutaQuery, IReadOnlyList<HojaDeRutaResumenDto>>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IVehiculoRepository _vehiculoRepository;
    private readonly IRepartidorRepository _repartidorRepository;
    private readonly ICurrentTenant _currentTenant;

    public ListarHojasDeRutaQueryHandler(
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

    public async Task<IReadOnlyList<HojaDeRutaResumenDto>> Handle(ListarHojasDeRutaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Siempre se filtra por el operador del usuario autenticado: nunca se consulta entre operadores
        var rutas = await _rutaRepository.ListarAsync(
            _currentTenant.OperadorId, request.Fecha, request.RepartidorId, request.Estados, cancellationToken);

        if (rutas.Count == 0)
            return Array.Empty<HojaDeRutaResumenDto>();

        var envios = (await _envioRepository.GetByIdsAsync(rutas.SelectMany(r => r.Paradas).Select(p => p.EnvioId), cancellationToken))
            .Where(e => e.OperadorId == _currentTenant.OperadorId)
            .ToDictionary(e => e.Id);
        var vehiculos = (await _vehiculoRepository.GetByIdsAsync(
                rutas.Where(r => r.VehiculoId.HasValue).Select(r => r.VehiculoId!.Value), cancellationToken))
            .ToDictionary(v => v.Id);
        var repartidores = (await _repartidorRepository.GetByIdsAsync(
                rutas.Where(r => r.RepartidorId.HasValue).Select(r => r.RepartidorId!.Value), cancellationToken))
            .ToDictionary(r => r.Id);

        return rutas.Select(r => HojaDeRutaMapper.Resumir(r, envios, vehiculos, repartidores)).ToList();
    }
}
