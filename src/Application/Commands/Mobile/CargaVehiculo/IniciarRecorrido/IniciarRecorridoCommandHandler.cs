using MediatR;
using UltimaMilla.Application.Events;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mobile.CargaVehiculo.IniciarRecorrido;

public class IniciarRecorridoCommandHandler : IRequestHandler<IniciarRecorridoCommand, IniciarRecorridoResultDto>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboxStorage _outboxStorage;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly ITableroRealtimeNotifier? _tableroNotifier;

    public IniciarRecorridoCommandHandler(
        IRutaRepository rutaRepository,
        IUnitOfWork unitOfWork,
        IOutboxStorage outboxStorage,
        ICurrentTenant currentTenant,
        IClock clock,
        ITableroRealtimeNotifier? tableroNotifier = null)
    {
        _rutaRepository = rutaRepository;
        _unitOfWork = unitOfWork;
        _outboxStorage = outboxStorage;
        _currentTenant = currentTenant;
        _clock = clock;
        _tableroNotifier = tableroNotifier;
    }

    public async Task<IniciarRecorridoResultDto> Handle(IniciarRecorridoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");

        // Anti-IDOR: la ruta debe pertenecer al operador del usuario autenticado
        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: La hoja de ruta no corresponde al operador actual.");

        // Idempotencia: si ya está en tránsito, informar sin repetir
        if (ruta.Estado == "EnTransito")
        {
            return new IniciarRecorridoResultDto(
                ruta.Id,
                ruta.Estado,
                _clock.UtcNow,
                true,
                "La hoja de ruta ya se encontraba en tránsito (operación idempotente).");
        }

        if (ruta.Estado != "Despachada")
        {
            throw new DomainValidationException($"Solo se puede iniciar el recorrido de una hoja de ruta en estado 'Despachada' (Estado actual: '{ruta.Estado}').");
        }

        ruta.IniciarRecorrido();

        var outboxMessage = EventosIntegracion.DeRuta(
            EventosIntegracion.Tipos.RutaIniciada,
            ruta,
            new
            {
                HojaDeRutaId = ruta.Id,
                FechaInicioUtc = _clock.UtcNow,
                IdempotencyKey = request.IdempotencyKey
            });

        await _rutaRepository.UpdateAsync(ruta, cancellationToken);
        await _outboxStorage.SaveAsync(outboxMessage, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_tableroNotifier is not null)
        {
            try
            {
                await _tableroNotifier.NotificarCambioRutaAsync(ruta.OperadorId, ruta.Id, ruta.Estado, cancellationToken);
            }
            catch
            {
                // Silente ante fallo de WebSocket
            }
        }

        return new IniciarRecorridoResultDto(
            ruta.Id,
            ruta.Estado,
            _clock.UtcNow,
            false,
            "Recorrido iniciado exitosamente. Hoja de ruta en tránsito.");
    }
}

