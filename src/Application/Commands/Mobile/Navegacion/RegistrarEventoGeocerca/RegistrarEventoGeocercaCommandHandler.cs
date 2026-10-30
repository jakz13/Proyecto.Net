using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mobile.Navegacion.RegistrarEventoGeocerca;

public class RegistrarEventoGeocercaCommandHandler : IRequestHandler<RegistrarEventoGeocercaCommand, EventoGeocercaResultDto>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IOutboxStorage _outboxStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDistributedCacheService _cacheService;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    public RegistrarEventoGeocercaCommandHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IOutboxStorage outboxStorage,
        IUnitOfWork unitOfWork,
        IDistributedCacheService cacheService,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _outboxStorage = outboxStorage;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    public async Task<EventoGeocercaResultDto> Handle(RegistrarEventoGeocercaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        if (request.ParadaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la parada es inválido.");

        if (string.IsNullOrWhiteSpace(request.TipoEvento))
            throw new DomainValidationException("El tipo de evento de geocerca es requerido.");

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");

        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: la hoja de ruta no corresponde al operador actual.");

        var parada = ruta.Paradas.FirstOrDefault(p => p.Id == request.ParadaId)
            ?? throw new NotFoundException($"La parada '{request.ParadaId}' no pertenece a la hoja de ruta '{request.HojaDeRutaId}'.");

        var envioId = request.EnvioId != Guid.Empty ? request.EnvioId : parada.EnvioId;
        if (parada.EnvioId != envioId)
            throw new DomainValidationException($"La parada '{request.ParadaId}' no corresponde al envío '{envioId}'.");

        var envio = await _envioRepository.GetByIdAsync(envioId, cancellationToken)
            ?? throw new NotFoundException($"Envío '{envioId}' no encontrado.");

        if (envio.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el envío no corresponde al operador actual.");

        var (evento, idempotente) = envio.RegistrarEventoGeocerca(
            request.TipoEvento,
            request.Latitud,
            request.Longitud,
            request.DistanciaMetros,
            request.TiempoPermanenciaSegundos,
            _currentTenant.UsuarioId);

        var timestamp = request.TimestampUtc ?? evento.Timestamp;

        if (!idempotente)
        {
            var payloadIntegration = JsonSerializer.Serialize(new
            {
                EnvioId = envio.Id,
                HojaDeRutaId = ruta.Id,
                ParadaId = parada.Id,
                CodigoSeguimiento = envio.CodigoSeguimientoPublico,
                TipoEvento = evento.TipoEvento,
                Descripcion = evento.Payload,
                Latitud = request.Latitud,
                Longitud = request.Longitud,
                DistanciaMetros = request.DistanciaMetros,
                TiempoPermanenciaSegundos = request.TiempoPermanenciaSegundos,
                TimestampUtc = timestamp
            });

            await _outboxStorage.SaveAsync(new OutboxMessage(
                Guid.NewGuid(),
                nameof(Domain.Entities.Envios.Envio),
                envio.Id,
                "EventoGeocercaRegistradoIntegrationEvent",
                payloadIntegration), cancellationToken);

            await _envioRepository.UpdateAsync(envio, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Invalidar caché de seguimiento público para reflejar de inmediato el evento de geocerca
        await _cacheService.RemoveAsync($"seguimiento:{envio.CodigoSeguimientoPublico}", cancellationToken);
        await _cacheService.RemoveAsync($"envio:{envio.Id}", cancellationToken);

        return new EventoGeocercaResultDto(
            envio.Id,
            ruta.Id,
            parada.Id,
            envio.CodigoSeguimientoPublico,
            evento.TipoEvento,
            evento.Payload ?? string.Empty,
            request.DistanciaMetros,
            request.TiempoPermanenciaSegundos,
            timestamp,
            idempotente);
    }
}

