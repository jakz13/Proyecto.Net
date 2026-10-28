using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Tablero.EnviarInstruccionRepartidor;

public class EnviarInstruccionRepartidorCommandHandler : IRequestHandler<EnviarInstruccionRepartidorCommand, bool>
{
    private readonly IRepartidorRepository _repartidorRepository;
    private readonly IOutboxStorage _outboxStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    public EnviarInstruccionRepartidorCommandHandler(
        IRepartidorRepository repartidorRepository,
        IOutboxStorage outboxStorage,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _repartidorRepository = repartidorRepository;
        _outboxStorage = outboxStorage;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    public async Task<bool> Handle(EnviarInstruccionRepartidorCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RepartidorId == Guid.Empty)
            throw new DomainValidationException("El identificador del repartidor no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(request.Mensaje))
            throw new DomainValidationException("El mensaje de instrucción operativa no puede ser vacío.");

        var repartidor = await _repartidorRepository.GetByIdAsync(request.RepartidorId, cancellationToken)
            ?? throw new NotFoundException($"No se encontró el repartidor '{request.RepartidorId}'.");

        if (repartidor.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("El repartidor no pertenece al operador de la sesión actual.");

        var payload = JsonSerializer.Serialize(new
        {
            RepartidorId = repartidor.Id,
            Mensaje = request.Mensaje.Trim(),
            OperadorId = _currentTenant.OperadorId,
            EmisorUsuarioId = _currentTenant.UsuarioId,
            FechaUtc = _clock.UtcNow
        });

        var outboxMessage = new OutboxMessage(
            Guid.NewGuid(),
            nameof(Repartidor),
            repartidor.Id,
            "InstruccionOperativaEnviadaIntegrationEvent",
            payload);

        await _outboxStorage.SaveAsync(outboxMessage, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
