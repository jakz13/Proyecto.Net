using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Comercio.ConfirmarDevolucion;

public class ConfirmarDevolucionCommandHandler : IRequestHandler<ConfirmarDevolucionCommand, ConfirmarDevolucionResultDto>
{
    private readonly IEnvioRepository _envioRepository;
    private readonly IDevolucionRepository _devolucionRepository;
    private readonly IOutboxStorage _outboxStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    public ConfirmarDevolucionCommandHandler(
        IEnvioRepository envioRepository,
        IDevolucionRepository devolucionRepository,
        IOutboxStorage outboxStorage,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _envioRepository = envioRepository;
        _devolucionRepository = devolucionRepository;
        _outboxStorage = outboxStorage;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    public async Task<ConfirmarDevolucionResultDto> Handle(ConfirmarDevolucionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EnvioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío es inválido.");

        var comercioId = _currentTenant.ComercioOperadorId
            ?? throw new UnauthorizedAccessException("Acceso denegado: el usuario no pertenece a un comercio.");

        var envio = await _envioRepository.GetByIdAsync(request.EnvioId, cancellationToken)
            ?? throw new NotFoundException($"Envío '{request.EnvioId}' no encontrado.");

        // Anti-IDOR: el envío debe ser del operador y del comercio del usuario autenticado
        if (envio.OperadorId != _currentTenant.OperadorId || envio.ComercioOperadorId != comercioId)
            throw new UnauthorizedAccessException("Acceso denegado: el envío no pertenece al comercio del usuario.");

        var devolucion = await _devolucionRepository.GetByEnvioIdAsync(envio.Id, cancellationToken);

        // Idempotencia: confirmar una devolución ya cerrada no repite transiciones
        if (envio.EstadoActual == EstadoEnvio.Devuelto)
        {
            return new ConfirmarDevolucionResultDto(
                envio.Id, envio.CodigoSeguimientoPublico, envio.EstadoActual.ToString(), devolucion?.Estado ?? Devolucion.EstadoDevuelto,
                true, "La devolución ya había sido confirmada (operación idempotente).");
        }

        envio.ConfirmarDevolucionRecibida(_currentTenant.UsuarioId);

        if (devolucion is null)
        {
            devolucion = new Devolucion(Guid.NewGuid(), envio.Id, estado: Devolucion.EstadoEnDevolucion);
            await _devolucionRepository.AddAsync(devolucion, cancellationToken);
        }

        devolucion.Rendir();

        await _outboxStorage.SaveAsync(new OutboxMessage(
            Guid.NewGuid(), nameof(Domain.Entities.Envios.Envio), envio.Id, "EnvioDevueltoIntegrationEvent",
            JsonSerializer.Serialize(new { EnvioId = envio.Id, Estado = EstadoEnvio.Devuelto.ToString(), Timestamp = _clock.UtcNow })),
            cancellationToken);

        await _envioRepository.UpdateAsync(envio, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ConfirmarDevolucionResultDto(
            envio.Id, envio.CodigoSeguimientoPublico, envio.EstadoActual.ToString(), devolucion.Estado,
            false, "Recepción confirmada: la devolución quedó cerrada.");
    }
}
