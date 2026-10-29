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

    public RegistrarPosicionesCommandHandler(
        IRepartidorRepository repartidorRepository,
        IRutaRepository rutaRepository,
        IPosicionVehiculoRepository posicionRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _repartidorRepository = repartidorRepository;
        _rutaRepository = rutaRepository;
        _posicionRepository = posicionRepository;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    public async Task<RegistrarPosicionesResultDto> Handle(RegistrarPosicionesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Posiciones is null || request.Posiciones.Count == 0)
            throw new DomainValidationException("Debe informar al menos una posición.");

        if (request.Posiciones.Count > MaximoPorLote)
            throw new DomainValidationException($"Un lote no puede superar las {MaximoPorLote} posiciones.");

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

            var vehiculoDeLaRuta = ruta.VehiculoId ?? repartidor.VehiculoAsignadoId
                ?? throw new DomainValidationException($"La hoja de ruta '{rutaId}' no tiene vehículo asignado.");
            vehiculosPorRuta[rutaId] = vehiculoDeLaRuta;
        }

        var ahora = _clock.UtcNow;
        var posiciones = new List<PosicionVehiculo>(request.Posiciones.Count);
        foreach (var p in request.Posiciones)
        {
            Guid vehiculoId;
            if (p.HojaDeRutaId.HasValue)
                vehiculoId = vehiculosPorRuta[p.HojaDeRutaId.Value];
            else
                vehiculoId = repartidor.VehiculoAsignadoId
                    ?? throw new DomainValidationException("El repartidor no tiene un vehículo asignado: indique la hoja de ruta de la posición.");

            // Un reloj adelantado del dispositivo no debe colocar la posición en el futuro
            var tomadaEn = p.TomadaEn is { } t && t <= ahora ? t : ahora;

            posiciones.Add(new PosicionVehiculo(
                Guid.NewGuid(), _currentTenant.OperadorId, vehiculoId, p.Latitud, p.Longitud, OrigenMovil,
                p.HojaDeRutaId, repartidor.Id, p.VelocidadKmh, p.BateriaPct, tomadaEn));
        }

        await _posicionRepository.AddRangeAsync(posiciones, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegistrarPosicionesResultDto(posiciones.Count);
    }
}
