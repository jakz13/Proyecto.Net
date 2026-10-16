using UltimaMilla.Domain.Entities.Mensajeria;

namespace UltimaMilla.Application.Ports;

public interface IOutboxStorage
{
    Task SaveAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task SaveRangeAsync(IEnumerable<OutboxMessage> messages, CancellationToken cancellationToken = default);
}
