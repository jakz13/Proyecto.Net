using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Seguimiento;

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

    public async Task<List<Destinatario>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idSet = ids.ToHashSet();
        return await _dbContext.Destinatarios.AsNoTracking().Where(d => idSet.Contains(d.Id)).ToListAsync(cancellationToken);
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

    public async Task<List<Direccion>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idSet = ids.ToHashSet();
        return await _dbContext.Direcciones.AsNoTracking().Where(d => idSet.Contains(d.Id)).ToListAsync(cancellationToken);
    }
}

public class EfComercioOperadorRepository : IComercioOperadorRepository
{
    private readonly AppDbContext _dbContext;

    public EfComercioOperadorRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ComercioOperador?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ComerciosOperadores.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<ComercioOperador?> GetByHuellaClaveApiAsync(string huella, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ComerciosOperadores.AsNoTracking().FirstOrDefaultAsync(c => c.ClaveApi == huella, cancellationToken);
    }
}

public class EfLoteImportacionRepository : ILoteImportacionRepository
{
    private readonly AppDbContext _dbContext;

    public EfLoteImportacionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LoteImportacion?> GetByClaveAsync(Guid comercioOperadorId, string claveIdempotencia, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LotesImportacion
            .FirstOrDefaultAsync(l => l.ComercioOperadorId == comercioOperadorId && l.ClaveIdempotencia == claveIdempotencia, cancellationToken);
    }

    public async Task AddAsync(LoteImportacion lote, CancellationToken cancellationToken = default)
    {
        await _dbContext.LotesImportacion.AddAsync(lote, cancellationToken);
    }
}

public class EfSolicitudReprogramacionRepository : ISolicitudReprogramacionRepository
{
    private readonly AppDbContext _dbContext;

    public EfSolicitudReprogramacionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(SolicitudReprogramacion solicitud, CancellationToken cancellationToken = default)
    {
        await _dbContext.SolicitudesReprogramacion.AddAsync(solicitud, cancellationToken);
    }

    public async Task<int> ContarAprobadasAsync(Guid envioId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SolicitudesReprogramacion.CountAsync(s => s.EnvioId == envioId && s.Estado == "Aprobada", cancellationToken);
    }
}
