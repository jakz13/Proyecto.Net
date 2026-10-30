using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Seguimiento;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public class EfSuscripcionAvisoRepository : ISuscripcionAvisoRepository
{
    private readonly AppDbContext _context;

    public EfSuscripcionAvisoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SuscripcionAviso>> ListarPorComercioOperadorAsync(Guid comercioOperadorId, CancellationToken cancellationToken = default)
    {
        return await _context.SuscripcionesAvisos
            .Where(s => s.ComercioOperadorId == comercioOperadorId)
            .ToListAsync(cancellationToken);
    }

    public async Task<SuscripcionAviso?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SuscripcionesAvisos.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task AddAsync(SuscripcionAviso suscripcion, CancellationToken cancellationToken = default)
    {
        await _context.SuscripcionesAvisos.AddAsync(suscripcion, cancellationToken);
    }

    public Task RemoveRangeAsync(IEnumerable<SuscripcionAviso> suscripciones, CancellationToken cancellationToken = default)
    {
        _context.SuscripcionesAvisos.RemoveRange(suscripciones);
        return Task.CompletedTask;
    }
}

public class EfAvisoComercioRepository : IAvisoComercioRepository
{
    private readonly AppDbContext _context;

    public EfAvisoComercioRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AvisoComercio>> ListarRecientesPorComercioAsync(Guid comercioOperadorId, int limite = 10, CancellationToken cancellationToken = default)
    {
        var idsSuscripcion = await _context.SuscripcionesAvisos
            .Where(s => s.ComercioOperadorId == comercioOperadorId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (idsSuscripcion.Count == 0)
            return Array.Empty<AvisoComercio>();

        return await _context.AvisosComercios
            .Where(a => idsSuscripcion.Contains(a.SuscripcionAvisoId))
            .OrderByDescending(a => a.Timestamp)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AvisoComercio>> ListarHistorialPorComercioAsync(Guid comercioOperadorId, string? estado = null, Guid? envioId = null, CancellationToken cancellationToken = default)
    {
        var idsSuscripcion = await _context.SuscripcionesAvisos
            .Where(s => s.ComercioOperadorId == comercioOperadorId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (idsSuscripcion.Count == 0)
            return Array.Empty<AvisoComercio>();

        var consulta = _context.AvisosComercios
            .Where(a => idsSuscripcion.Contains(a.SuscripcionAvisoId));

        if (!string.IsNullOrWhiteSpace(estado))
        {
            var estadoFiltro = estado.Trim();
            consulta = consulta.Where(a => a.Estado == estadoFiltro);
        }

        if (envioId.HasValue && envioId.Value != Guid.Empty)
        {
            consulta = consulta.Where(a => a.EnvioId == envioId.Value);
        }

        return await consulta
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<AvisoComercio?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AvisosComercios.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task AddAsync(AvisoComercio aviso, CancellationToken cancellationToken = default)
    {
        await _context.AvisosComercios.AddAsync(aviso, cancellationToken);
    }

    public Task UpdateAsync(AvisoComercio aviso, CancellationToken cancellationToken = default)
    {
        _context.AvisosComercios.Update(aviso);
        return Task.CompletedTask;
    }
}
