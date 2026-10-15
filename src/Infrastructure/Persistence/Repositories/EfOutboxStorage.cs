using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Mensajeria;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public class EfOutboxStorage : IOutboxStorage
{
    private readonly AppDbContext _dbContext;

    public EfOutboxStorage(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _dbContext.OutboxMessages.AddAsync(message, cancellationToken);
    }

    public async Task SaveRangeAsync(IEnumerable<OutboxMessage> messages, CancellationToken cancellationToken = default)
    {
        await _dbContext.OutboxMessages.AddRangeAsync(messages, cancellationToken);
    }
}
