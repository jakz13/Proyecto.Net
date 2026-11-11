using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Planificacion;
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
    private readonly IRetiroRepository? _retiroRepository;

    public ObtenerHojaDeRutaQueryHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IVehiculoRepository vehiculoRepository,
        IRepartidorRepository repartidorRepository,
        IFranjaHorariaRepository franjaHorariaRepository,
        ICurrentTenant currentTenant,
        IRetiroRepository? retiroRepository = null)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _vehiculoRepository = vehiculoRepository;
        _repartidorRepository = repartidorRepository;
        _franjaHorariaRepository = franjaHorariaRepository;
        _currentTenant = currentTenant;
        _retiroRepository = retiroRepository;
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
        var envios = (await _envioRepository.GetByIdsAsync(paradas.IdsDeEnvios(), cancellationToken))
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

        var idsRetiros = paradas.Where(p => p.RetiroId.HasValue).Select(p => p.RetiroId!.Value).Distinct().ToList();
        var retiros = (_retiroRepository is not null && idsRetiros.Count > 0)
            ? (await _retiroRepository.GetByIdsAsync(idsRetiros, cancellationToken))
                .Where(r => r.OperadorId == _currentTenant.OperadorId)
                .ToDictionary(r => r.Id)
            : new Dictionary<Guid, Domain.Entities.Retiros.Retiro>();

        var detalleParadas = new List<ParadaDetalleDto>();
        foreach (var p in paradas)
        {
            string? ventana = null;
            if (p.FranjaHorariaId.HasValue && franjas.TryGetValue(p.FranjaHorariaId.Value, out var franja))
                ventana = HojaDeRutaMapper.FormatearVentana(franja);

            if (p.EnvioId.HasValue && envios.TryGetValue(p.EnvioId.Value, out var envio))
            {
                detalleParadas.Add(new ParadaDetalleDto(
                    p.Id, p.Orden, envio.Id, envio.CodigoSeguimientoPublico, envio.EstadoActual.ToString(), p.Estado,
                    envio.NombreDestinatario, envio.DireccionDestino, envio.ZonaClasificadaId, ventana,
                    envio.PesoTotal, envio.VolumenTotal));
            }
            else if (p.RetiroId.HasValue && retiros.TryGetValue(p.RetiroId.Value, out var retiro))
            {
                detalleParadas.Add(new ParadaDetalleDto(
                    p.Id, p.Orden, retiro.Id, retiro.CodigoSeguimientoPublico, retiro.EstadoActual.ToString(), p.Estado,
                    retiro.NombreContacto, retiro.DireccionRetiro, null, ventana,
                    retiro.PesoTotal, retiro.VolumenTotal));
            }
        }

        return new HojaDeRutaDetalleDto(
            HojaDeRutaMapper.Resumir(ruta, envios, vehiculos, repartidores),
            detalleParadas);
    }
}
