using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Infrastructure.Persistence;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;

    public EfUnitOfWork(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictoDeConcurrenciaException(
                "Los datos fueron modificados por otra operación simultánea. Recargue la información e intente nuevamente.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictoDeConcurrenciaException(
                "Otra operación simultánea ya registró los mismos datos (por ejemplo, el envío ya fue asignado a otra hoja de ruta).", ex);
        }
    }
}
