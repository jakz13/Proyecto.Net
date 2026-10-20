using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Planificacion;

public class ObtenerHojaDeRutaQueryHandler : IRequestHandler<ObtenerHojaDeRutaQuery, HojaDeRutaDetalleDto>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IVehiculoRepository _vehiculoRepository;
    private readonly IRepartidorRepository _repartidorRepository;
    private readonly IFranjaHorariaRepository _franjaHorariaRepository;
    private readonly ICurrentTenant _currentTenant;

    public ObtenerHojaDeRutaQueryHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IVehiculoRepository vehiculoRepository,
        IRepartidorRepository repartidorRepository,
        IFranjaHorariaRepository franjaHorariaRepository,
        ICurrentTenant currentTenant)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _vehiculoRepository = vehiculoRepository;
        _repartidorRepository = repartidorRepository;
        _franjaHorariaRepository = franjaHorariaRepository;
        _currentTenant = currentTenant;
    }

    public async Task<HojaDeRutaDetalleDto> Handle(ObtenerHojaDeRutaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");

        // Anti-IDOR: la ruta debe pertenecer al operador del usuario autenticado
        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: la hoja de ruta no corresponde al operador actual.");

        var paradas = HojaDeRutaMapper.ParadasVigentes(ruta).ToList();
        var envios = (await _envioRepository.GetByIdsAsync(paradas.Select(p => p.EnvioId), cancellationToken))
            .Where(e => e.OperadorId == _currentTenant.OperadorId)
            .ToDictionary(e => e.Id);
        var vehiculos = (await _vehiculoRepository.GetByIdsAsync(
                ruta.VehiculoId.HasValue ? new[] { ruta.VehiculoId.Value } : Array.Empty<Guid>(), cancellationToken))
            .ToDictionary(v => v.Id);
        var repartidores = (await _repartidorRepository.GetByIdsAsync(
                ruta.RepartidorId.HasValue ? new[] { ruta.RepartidorId.Value } : Array.Empty<Guid>(), cancellationToken))
            .ToDictionary(r => r.Id);
        var franjas = (await _franjaHorariaRepository.GetByIdsAsync(
                paradas.Where(p => p.FranjaHorariaId.HasValue).Select(p => p.FranjaHorariaId!.Value), cancellationToken))
            .ToDictionary(f => f.Id);

        var detalleParadas = paradas
            .Where(p => envios.ContainsKey(p.EnvioId))
            .Select(p =>
            {
                var envio = envios[p.EnvioId];
                string? ventana = null;
                if (p.FranjaHorariaId.HasValue && franjas.TryGetValue(p.FranjaHorariaId.Value, out var franja))
                    ventana = HojaDeRutaMapper.FormatearVentana(franja);

                return new ParadaDetalleDto(
                    p.Id, p.Orden, envio.Id, envio.CodigoSeguimientoPublico, envio.EstadoActual.ToString(), p.Estado,
                    envio.NombreDestinatario, envio.DireccionDestino, envio.ZonaClasificadaId, ventana,
                    envio.PesoTotal, envio.VolumenTotal);
            })
            .ToList();

        return new HojaDeRutaDetalleDto(
            HojaDeRutaMapper.Resumir(ruta, envios, vehiculos, repartidores),
            detalleParadas);
    }
}
