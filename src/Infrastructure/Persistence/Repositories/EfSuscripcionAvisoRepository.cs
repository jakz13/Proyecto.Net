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
        return await _context.SuscripcionesAviso
            .Where(s => s.ComercioOperadorId == comercioOperadorId)
            .ToListAsync(cancellationToken);
    }

    public async Task<SuscripcionAviso?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SuscripcionesAviso.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task AddAsync(SuscripcionAviso suscripcion, CancellationToken cancellationToken = default)
    {
        await _context.SuscripcionesAviso.AddAsync(suscripcion, cancellationToken);
    }

    public Task RemoveRangeAsync(IEnumerable<SuscripcionAviso> suscripciones, CancellationToken cancellationToken = default)
    {
        _context.SuscripcionesAviso.RemoveRange(suscripciones);
        return Task.CompletedTask;
    }
}

public class EfAvisoComercioConsultaRepository : IAvisoComercioConsultaRepository
{
    private readonly AppDbContext _context;

    public EfAvisoComercioConsultaRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AvisoComercio>> ListarRecientesPorComercioAsync(Guid comercioOperadorId, int limite = 10, CancellationToken cancellationToken = default)
    {
        var idsSuscripcion = await _context.SuscripcionesAviso
            .Where(s => s.ComercioOperadorId == comercioOperadorId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (idsSuscripcion.Count == 0)
            return Array.Empty<AvisoComercio>();

        return await _context.AvisosComercio
            .Where(a => idsSuscripcion.Contains(a.SuscripcionAvisoId))
            .OrderByDescending(a => a.Timestamp)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AvisoComercio>> ListarHistorialPorComercioAsync(Guid comercioOperadorId, string? estado = null, Guid? envioId = null, CancellationToken cancellationToken = default)
    {
        var idsSuscripcion = await _context.SuscripcionesAviso
            .Where(s => s.ComercioOperadorId == comercioOperadorId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (idsSuscripcion.Count == 0)
            return Array.Empty<AvisoComercio>();

        var consulta = _context.AvisosComercio
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
        return await _context.AvisosComercio.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task AddAsync(AvisoComercio aviso, CancellationToken cancellationToken = default)
    {
        await _context.AvisosComercio.AddAsync(aviso, cancellationToken);
    }

    public Task UpdateAsync(AvisoComercio aviso, CancellationToken cancellationToken = default)
    {
        _context.AvisosComercio.Update(aviso);
        return Task.CompletedTask;
    }
}
