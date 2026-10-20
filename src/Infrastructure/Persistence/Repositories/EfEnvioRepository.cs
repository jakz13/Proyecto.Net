using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Enums;

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

    public async Task<List<Envio>> ListarPorEstadoAsync(Guid operadorId, EstadoEnvio estado, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Envios
            .AsNoTracking()
            .Where(e => e.OperadorId == operadorId && e.EstadoActual == estado)
            .OrderBy(e => e.FechaAlta)
            .ToListAsync(cancellationToken);
    }

    public async Task<Envio?> GetByCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default)
    {
        var codigo = codigoSeguimiento.Trim().ToUpperInvariant();
        return await _dbContext.Envios
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.CodigoSeguimientoPublico == codigo, cancellationToken);
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
        // Una entidad ya rastreada detecta sola sus cambios; Update marcaría como modificados los hijos nuevos (eventos, intentos) y fallaría al guardar
        if (_dbContext.Entry(envio).State == EntityState.Detached)
            _dbContext.Envios.Update(envio);

        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IEnumerable<Envio> envios, CancellationToken cancellationToken = default)
    {
        foreach (var envio in envios)
        {
            if (_dbContext.Entry(envio).State == EntityState.Detached)
                _dbContext.Envios.Update(envio);
        }

        return Task.CompletedTask;
    }

    public async Task<ReglaOperativa?> GetReglaOperativaVigenteAsync(Guid operadorId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ReglasOperativas
            .Where(r => r.OperadorId == operadorId && (r.VigenteHasta == null || r.VigenteHasta > DateTime.UtcNow))
            .OrderByDescending(r => r.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<MotivoNoEntrega>> ListarMotivosNoEntregaActivosAsync(Guid operadorId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MotivosNoEntrega
            .AsNoTracking()
            .Where(m => m.OperadorId == operadorId && m.Activo && !m.EsInterno)
            .OrderBy(m => m.Codigo)
            .ToListAsync(cancellationToken);
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
