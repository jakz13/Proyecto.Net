using MediatR;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mensajeria.PublicarOutboxPendiente;

public class PublicarOutboxPendienteCommandHandler : IRequestHandler<PublicarOutboxPendienteCommand, ResultadoPublicacionOutbox>
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly IMessagePublisher _publisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public PublicarOutboxPendienteCommandHandler(
        IOutboxRepository outboxRepository,
        IMessagePublisher publisher,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _outboxRepository = outboxRepository;
        _publisher = publisher;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<ResultadoPublicacionOutbox> Handle(PublicarOutboxPendienteCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TamanoLote <= 0)
            throw new DomainValidationException("El tamaño del lote del outbox debe ser mayor a cero.");

        var pendientes = await _outboxRepository.ListarPendientesAsync(request.TamanoLote, cancellationToken);
        var publicados = 0;

        foreach (var mensaje in pendientes)
        {
            try
            {
                await _publisher.PublicarAsync(
                    new MensajeSaliente(
                        mensaje.Id,
                        mensaje.AgregadoOrigen,
                        mensaje.AgregadoId,
                        mensaje.TipoEvento,
                        mensaje.Payload,
                        PayloadMensaje.CorrelationIdDe(mensaje.Payload)),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Si el broker no confirma, el mensaje queda sin marcar y se retoma en la próxima vuelta: no se pierde ni se adelanta el orden
                return new ResultadoPublicacionOutbox(publicados, pendientes.Count - publicados, ex.Message);
            }

            // Se confirma uno a uno para minimizar la ventana de reenvío si falla el guardado (el consumidor es idempotente)
            mensaje.MarcarProcesado(_clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            publicados++;
        }

        return new ResultadoPublicacionOutbox(publicados, 0, null);
    }
}
