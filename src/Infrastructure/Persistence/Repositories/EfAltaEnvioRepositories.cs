using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public class EfCuadroTarifarioRepository : ICuadroTarifarioRepository
{
    private readonly AppDbContext _dbContext;

    public EfCuadroTarifarioRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CuadroTarifario?> GetVigenteAsync(Guid operadorId, DateTime fechaUtc, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CuadrosTarifarios
            .AsNoTracking()
            .Where(c => c.OperadorId == operadorId
                        && c.VigenteDesde <= fechaUtc
                        && (c.VigenteHasta == null || c.VigenteHasta >= fechaUtc))
            .OrderByDescending(c => c.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public class EfDestinatarioRepository : IDestinatarioRepository
{
    private readonly AppDbContext _dbContext;

    public EfDestinatarioRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Destinatario destinatario, CancellationToken cancellationToken = default)
    {
        await _dbContext.Destinatarios.AddAsync(destinatario, cancellationToken);
    }
}

public class EfDireccionRepository : IDireccionRepository
{
    private readonly AppDbContext _dbContext;

    public EfDireccionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Direccion direccion, CancellationToken cancellationToken = default)
    {
        await _dbContext.Direcciones.AddAsync(direccion, cancellationToken);
    }
}
