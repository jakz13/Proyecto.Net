using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mobile.Posicion.RegistrarPosiciones;

public class RegistrarPosicionesCommandHandler : IRequestHandler<RegistrarPosicionesCommand, RegistrarPosicionesResultDto>
{
    public const int MaximoPorLote = 500;
    private const string OrigenMovil = "Movil";

    private readonly IRepartidorRepository _repartidorRepository;
    private readonly IRutaRepository _rutaRepository;
    private readonly IPosicionVehiculoRepository _posicionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly ITableroRealtimeNotifier? _tableroNotifier;
    private readonly IVehiculoRepository? _vehiculoRepository;

    public RegistrarPosicionesCommandHandler(
        IRepartidorRepository repartidorRepository,
        IRutaRepository rutaRepository,
        IPosicionVehiculoRepository posicionRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IClock clock,
        ITableroRealtimeNotifier? tableroNotifier = null,
        IVehiculoRepository? vehiculoRepository = null)
    {
        _repartidorRepository = repartidorRepository;
        _rutaRepository = rutaRepository;
        _posicionRepository = posicionRepository;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _clock = clock;
        _tableroNotifier = tableroNotifier;
        _vehiculoRepository = vehiculoRepository;
    }

    public async Task<RegistrarPosicionesResultDto> Handle(RegistrarPosicionesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Posiciones is null || request.Posiciones.Count == 0)
            throw new DomainValidationException("Debe informar al menos una posición.");

        if (request.Posiciones.Count > MaximoPorLote)
            throw new DomainValidationException($"Un lote no puede superar las {MaximoPorLote} posiciones.");

        // {"posiciones":[null]} o {"posiciones":[{}]} son datos mal formados: sin latitud y longitud explícitas la posición quedaría en (0, 0)
        if (request.Posiciones.Any(p => p is null))
            throw new DomainValidationException("Cada posición debe indicar su latitud y su longitud.");
        if (request.Posiciones.Any(p => p.Latitud == 0 && p.Longitud == 0))
            throw new DomainValidationException("La posición (0, 0) no es válida: informe la latitud y la longitud tomadas por el dispositivo.");

        var usuarioId = _currentTenant.UsuarioId
            ?? throw new UnauthorizedAccessException("Acceso denegado: no se pudo identificar al usuario.");

        // El repartidor sale del usuario autenticado, nunca de un identificador recibido
        var repartidor = await _repartidorRepository.GetByUsuarioIdAsync(usuarioId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Acceso denegado: el usuario autenticado no es un repartidor.");
        if (repartidor.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el repartidor no corresponde al operador actual.");

        // Cada ruta mencionada debe ser del repartidor; su vehículo es el que se está siguiendo
        var vehiculosPorRuta = new Dictionary<Guid, Guid>();
        foreach (var rutaId in request.Posiciones.Where(p => p.HojaDeRutaId.HasValue).Select(p => p.HojaDeRutaId!.Value).Distinct())
        {
            var ruta = await _rutaRepository.GetByIdAsync(rutaId, cancellationToken)
                ?? throw new NotFoundException($"Hoja de ruta '{rutaId}' no encontrada.");
            if (ruta.OperadorId != _currentTenant.OperadorId || ruta.RepartidorId != repartidor.Id)
                throw new UnauthorizedAccessException("Acceso denegado: la hoja de ruta no corresponde al repartidor.");

            var vehiculoDeLaRuta = ruta.VehiculoId ?? repartidor.VehiculoAsignadoId;
            if (!vehiculoDeLaRuta.HasValue && _vehiculoRepository != null)
            {
                var vehiculoFallback = await _vehiculoRepository.ObtenerPrimerVehiculoActivoAsync(_currentTenant.OperadorId, cancellationToken);
                vehiculoDeLaRuta = vehiculoFallback?.Id;
            }

            if (!vehiculoDeLaRuta.HasValue)
                throw new DomainValidationException($"La hoja de ruta '{rutaId}' no tiene vehículo asignado.");

            vehiculosPorRuta[rutaId] = vehiculoDeLaRuta.Value;
        }

        var ahora = _clock.UtcNow;
        var posiciones = new List<PosicionVehiculo>(request.Posiciones.Count);
        foreach (var p in request.Posiciones)
        {
            Guid vehiculoId;
            if (p.HojaDeRutaId.HasValue && vehiculosPorRuta.TryGetValue(p.HojaDeRutaId.Value, out var vRuta))
            {
                vehiculoId = vRuta;
            }
            else
            {
                var vId = repartidor.VehiculoAsignadoId;
                if (!vId.HasValue && _vehiculoRepository != null)
                {
                    var vehiculoFallback = await _vehiculoRepository.ObtenerPrimerVehiculoActivoAsync(_currentTenant.OperadorId, cancellationToken);
                    vId = vehiculoFallback?.Id;
                }

                if (!vId.HasValue)
                    throw new DomainValidationException("El repartidor no tiene un vehículo asignado: indique la hoja de ruta de la posición.");

                vehiculoId = vId.Value;
            }

            // Un reloj adelantado del dispositivo no debe colocar la posición en el futuro
            var tomadaEn = p.TomadaEn is { } t && t <= ahora ? t : ahora;

            posiciones.Add(new PosicionVehiculo(
                Guid.NewGuid(), _currentTenant.OperadorId, vehiculoId, p.Latitud, p.Longitud, OrigenMovil,
                p.HojaDeRutaId, repartidor.Id, p.VelocidadKmh, p.BateriaPct, tomadaEn));
        }

        await _posicionRepository.AddRangeAsync(posiciones, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Tiempo real: una sola notificación por vehículo y lote, con la posición más reciente; no condiciona el resultado
        if (_tableroNotifier is not null)
        {
            foreach (var ultima in posiciones.GroupBy(x => x.VehiculoId).Select(g => g.OrderByDescending(x => x.Timestamp).First()))
            {
                await _tableroNotifier.NotificarPosicionVehiculoAsync(
                    _currentTenant.OperadorId, ultima.VehiculoId, ultima.HojaDeRutaId, ultima.Latitud, ultima.Longitud, ultima.VelocidadKmh, ultima.BateriaPct, cancellationToken);
            }
        }

        return new RegistrarPosicionesResultDto(posiciones.Count);
    }
}
