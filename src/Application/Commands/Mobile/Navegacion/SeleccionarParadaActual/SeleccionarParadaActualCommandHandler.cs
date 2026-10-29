using System.Text.Json;
using MediatR;
using UltimaMilla.Application.DTOs.Mobile;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mobile.Navegacion.SeleccionarParadaActual;

public class SeleccionarParadaActualCommandHandler : IRequestHandler<SeleccionarParadaActualCommand, ParadaActualizadaDto>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboxStorage _outboxStorage;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly ITableroRealtimeNotifier? _tableroNotifier;

    public SeleccionarParadaActualCommandHandler(
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

    public async Task<ParadaActualizadaDto> Handle(SeleccionarParadaActualCommand request, CancellationToken cancellationToken)
    {
        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        if (request.ParadaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la parada es inválido.");

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");

        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: La hoja de ruta no corresponde al operador actual.");

        var paradaExistente = ruta.Paradas.FirstOrDefault(p => p.Id == request.ParadaId)
            ?? throw new NotFoundException($"La parada '{request.ParadaId}' no pertenece a la hoja de ruta.");

        var estadoAnterior = paradaExistente.Estado;
        Parada paradaAfectada;

        if (string.Equals(request.Accion, "RegistrarArribo", StringComparison.OrdinalIgnoreCase))
        {
            paradaAfectada = ruta.RegistrarArriboAParada(request.ParadaId);
        }
        else
        {
            paradaAfectada = ruta.IniciarNavegacionHaciaParada(request.ParadaId);
        }

        var timestamp = request.Timestamp ?? _clock.UtcNow;

        // Registrar evento en Outbox para difusión en tiempo real (SignalR) al Backoffice
        var payloadEvento = JsonSerializer.Serialize(new
        {
            HojaDeRutaId = ruta.Id,
            ParadaId = paradaAfectada.Id,
            RepartidorId = ruta.RepartidorId,
            VehiculoId = ruta.VehiculoId,
            EstadoAnterior = estadoAnterior,
            NuevoEstado = paradaAfectada.Estado,
            LatitudRepartidor = request.LatitudActual,
            LongitudRepartidor = request.LongitudActual,
            Timestamp = timestamp
        });

        var outboxMessage = new OutboxMessage(
            Guid.NewGuid(),
            nameof(Parada),
            paradaAfectada.Id,
            "ParadaNavegacionActualizadaIntegrationEvent",
            payloadEvento);

        await _rutaRepository.UpdateAsync(ruta, cancellationToken);
        await _outboxStorage.SaveAsync(outboxMessage, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_tableroNotifier is not null)
        {
            try
            {
                await _tableroNotifier.NotificarHitoParadaAsync(ruta.OperadorId, ruta.Id, paradaAfectada.Id, paradaAfectada.Estado, cancellationToken);
            }
            catch
            {
                // El fallo de notificación en tiempo real no revierte ni invalida el hito de parada ya persistido
            }
        }

        string mensaje = paradaAfectada.Estado == "EnSitio"
            ? "Arribo a la parada registrado exitosamente."
            : "Navegación hacia la parada iniciada exitosamente.";

        return new ParadaActualizadaDto(
            ruta.Id,
            paradaAfectada.Id,
            estadoAnterior,
            paradaAfectada.Estado,
            timestamp,
            mensaje
        );
    }
}
