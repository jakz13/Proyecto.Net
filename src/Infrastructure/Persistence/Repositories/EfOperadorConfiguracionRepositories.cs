using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public class EfReglaOperativaRepository : IReglaOperativaRepository
{
    private readonly AppDbContext _dbContext;

    public EfReglaOperativaRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(ReglaOperativa regla, CancellationToken cancellationToken = default) =>
        await _dbContext.ReglasOperativas.AddAsync(regla, cancellationToken);

    public Task<ReglaOperativa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.ReglasOperativas.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<ReglaOperativa?> ObtenerVigenteAsync(Guid operadorId, CancellationToken cancellationToken = default) =>
        _dbContext.ReglasOperativas
            .Where(r => r.OperadorId == operadorId && r.VigenteHasta == null)
            .OrderByDescending(r => r.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<ReglaOperativa>> ListarAsync(Guid operadorId, CancellationToken cancellationToken = default) =>
        _dbContext.ReglasOperativas.AsNoTracking()
            .Where(r => r.OperadorId == operadorId)
            .OrderByDescending(r => r.Version)
            .ToListAsync(cancellationToken);
}

public class EfMotivoNoEntregaRepository : IMotivoNoEntregaRepository
{
    private readonly AppDbContext _dbContext;

    public EfMotivoNoEntregaRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(MotivoNoEntrega motivo, CancellationToken cancellationToken = default) =>
        await _dbContext.MotivosNoEntrega.AddAsync(motivo, cancellationToken);

    public Task<MotivoNoEntrega?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.MotivosNoEntrega.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<bool> ExisteCodigoAsync(Guid operadorId, string codigo, CancellationToken cancellationToken = default) =>
        _dbContext.MotivosNoEntrega.AnyAsync(m => m.OperadorId == operadorId && m.Codigo == codigo, cancellationToken);

    public Task<List<MotivoNoEntrega>> ListarAsync(Guid operadorId, bool soloParaRepartidor, CancellationToken cancellationToken = default)
    {
        var consulta = _dbContext.MotivosNoEntrega.AsNoTracking().Where(m => m.OperadorId == operadorId);
        if (soloParaRepartidor)
            consulta = consulta.Where(m => m.Activo && !m.EsInterno);
        return consulta.OrderBy(m => m.Codigo).ToListAsync(cancellationToken);
    }
}