using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public class EfEnvioRepository : IEnvioRepository
{
    private readonly AppDbContext _dbContext;

    public EfEnvioRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Envio envio, CancellationToken cancellationToken = default)
    {
        await _dbContext.Envios.AddAsync(envio, cancellationToken);
    }

    public async Task<bool> ExisteCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default)
    {
        var codigo = codigoSeguimiento.Trim().ToUpperInvariant();
        return await _dbContext.Envios.AnyAsync(e => e.CodigoSeguimientoPublico == codigo, cancellationToken);
    }

    public async Task<List<Envio>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Envios
            .AsNoTracking()
            .OrderByDescending(e => e.FechaAlta)
            .ToListAsync(cancellationToken);
    }

    public async Task<Envio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Envios
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<List<Envio>> GetByHojaDeRutaIdAsync(Guid hojaDeRutaId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Envios
            .Where(e => e.HojaDeRutaId == hojaDeRutaId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Envio>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idSet = ids.ToHashSet();
        return await _dbContext.Envios
            .Where(e => idSet.Contains(e.Id))
            .ToListAsync(cancellationToken);
    }

    public Task UpdateAsync(Envio envio, CancellationToken cancellationToken = default)
    {
        _dbContext.Envios.Update(envio);
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IEnumerable<Envio> envios, CancellationToken cancellationToken = default)
    {
        _dbContext.Envios.UpdateRange(envios);
        return Task.CompletedTask;
    }

    public async Task<ReglaOperativa?> GetReglaOperativaVigenteAsync(Guid operadorId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ReglasOperativas
            .Where(r => r.OperadorId == operadorId && (r.VigenteHasta == null || r.VigenteHasta > DateTime.UtcNow))
            .OrderByDescending(r => r.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<MotivoNoEntrega?> GetMotivoNoEntregaAsync(Guid operadorId, Guid? motivoId, string? codigo = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MotivosNoEntrega.Where(m => m.OperadorId == operadorId);

        if (motivoId.HasValue && motivoId.Value != Guid.Empty)
        {
            return await query.FirstOrDefaultAsync(m => m.Id == motivoId.Value, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(codigo))
        {
            var codigoNormalizado = codigo.Trim().ToUpperInvariant();
            return await query.FirstOrDefaultAsync(m => m.Codigo == codigoNormalizado, cancellationToken);
        }

        return null;
    }
}
