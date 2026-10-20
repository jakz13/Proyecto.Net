using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mobile.DevolucionEnCalle.RegistrarRetiroDevolucion;

public class RegistrarRetiroDevolucionCommandHandler : IRequestHandler<RegistrarRetiroDevolucionCommand, RetiroDevolucionResultDto>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IDevolucionRepository _devolucionRepository;
    private readonly IOutboxStorage _outboxStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    public RegistrarRetiroDevolucionCommandHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IDevolucionRepository devolucionRepository,
        IOutboxStorage outboxStorage,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _devolucionRepository = devolucionRepository;
        _outboxStorage = outboxStorage;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    public async Task<RetiroDevolucionResultDto> Handle(RegistrarRetiroDevolucionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        if (request.ParadaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la parada es inválido.");

        if (request.EnvioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío es inválido.");

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new DomainValidationException("La clave de idempotencia es requerida.");

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");
        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: la hoja de ruta no corresponde al operador actual.");

        var parada = ruta.Paradas.FirstOrDefault(p => p.Id == request.ParadaId)
            ?? throw new NotFoundException($"La parada '{request.ParadaId}' no pertenece a la hoja de ruta '{request.HojaDeRutaId}'.");
        if (parada.EnvioId != request.EnvioId)
            throw new DomainValidationException($"La parada '{request.ParadaId}' no corresponde al envío '{request.EnvioId}'.");

        var envio = await _envioRepository.GetByIdAsync(request.EnvioId, cancellationToken)
            ?? throw new NotFoundException($"Envío '{request.EnvioId}' no encontrado.");
        if (envio.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el envío no corresponde al operador actual.");

        var fechaDispositivo = request.FechaHoraDispositivoUtc ?? _clock.UtcNow;
        var aplicado = envio.RegistrarRetiroParaDevolucion(
            _currentTenant.UsuarioId, request.Latitud, request.Longitud, fechaDispositivo, request.Observaciones);

        var devolucion = await _devolucionRepository.GetByEnvioIdAsync(envio.Id, cancellationToken);
        if (devolucion is null)
        {
            devolucion = new Devolucion(Guid.NewGuid(), envio.Id, estado: Devolucion.EstadoEnDevolucion);
            await _devolucionRepository.AddAsync(devolucion, cancellationToken);
        }

        if (aplicado)
        {
            devolucion.MarcarEnCaminoADeposito();

            await _outboxStorage.SaveAsync(new OutboxMessage(
                Guid.NewGuid(), nameof(Domain.Entities.Envios.Envio), envio.Id, "DevolucionEnCaminoIntegrationEvent",
                JsonSerializer.Serialize(new { EnvioId = envio.Id, HojaDeRutaId = ruta.Id, Timestamp = _clock.UtcNow })), cancellationToken);

            await _envioRepository.UpdateAsync(envio, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else if (devolucion.Estado == Devolucion.EstadoEnDevolucion)
        {
            // El envío ya tenía los bultos en retorno pero faltaba el registro de la devolución
            devolucion.MarcarEnCaminoADeposito();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var enRetorno = envio.Bultos.Count(b => b.EstadoBulto == "EnRetornoADeposito");
        return new RetiroDevolucionResultDto(
            envio.Id, envio.CodigoSeguimientoPublico, envio.EstadoActual.ToString(), devolucion.Estado, enRetorno, !aplicado,
            aplicado
                ? "Bulto retirado: está en camino de vuelta al depósito, pendiente de rendición."
                : "El retiro para devolución ya se encontraba registrado (operación idempotente).");
    }
}
