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

    public async Task<ComercioOperador?> ObtenerPorComercioYOperadorAsync(Guid comercioId, Guid operadorId, CancellationToken cancellationToken = default)
    {
        if (comercioId == Guid.Empty || operadorId == Guid.Empty)
            return null;

        return await _dbContext.ComerciosOperadores
            .FirstOrDefaultAsync(co => co.ComercioId == comercioId && co.OperadorId == operadorId, cancellationToken);
    }

    public async Task<bool> ExisteRelacionAsync(Guid comercioId, Guid operadorId, CancellationToken cancellationToken = default)
    {
        if (comercioId == Guid.Empty || operadorId == Guid.Empty)
            return false;

        return await _dbContext.ComerciosOperadores
            .AsNoTracking()
            .AnyAsync(co => co.ComercioId == comercioId && co.OperadorId == operadorId, cancellationToken);
    }

    public async Task AgregarAsync(ComercioOperador relacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relacion);
        await _dbContext.ComerciosOperadores.AddAsync(relacion, cancellationToken);
    }

    public Task ActualizarAsync(ComercioOperador relacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relacion);
        _dbContext.ComerciosOperadores.Update(relacion);
        return Task.CompletedTask;
    }

    public async Task<(IReadOnlyList<ComercioConRelacionOperador> Elementos, int Total)> ListarComerciosDelOperadorAsync(
        Guid operadorId,
        string? busqueda,
        bool? soloActivos,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default)
    {
        var consulta = from relacion in _dbContext.ComerciosOperadores.AsNoTracking()
                       join comercio in _dbContext.Comercios.AsNoTracking() on relacion.ComercioId equals comercio.Id
                       where relacion.OperadorId == operadorId
                       select new { Comercio = comercio, Relacion = relacion };

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var textoBusqueda = busqueda.Trim();
            var patron = "%" + textoBusqueda.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            consulta = consulta.Where(x =>
                EF.Functions.ILike(x.Comercio.RazonSocial, patron, "\\") ||
                EF.Functions.ILike(x.Comercio.NombreFantasia, patron, "\\") ||
                EF.Functions.ILike(x.Comercio.Documento, patron, "\\") ||
                EF.Functions.ILike(x.Comercio.Email, patron, "\\"));
        }

        if (soloActivos.HasValue)
        {
            consulta = soloActivos.Value
                ? consulta.Where(x => x.Relacion.Estado == "Activo")
                : consulta.Where(x => x.Relacion.Estado != "Activo");
        }

        var total = await consulta.CountAsync(cancellationToken);
        var elementos = await consulta
            .OrderBy(x => x.Comercio.RazonSocial)
            .ThenBy(x => x.Comercio.Id)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(x => new ComercioConRelacionOperador(x.Comercio, x.Relacion))
            .ToListAsync(cancellationToken);

        return (elementos, total);
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

    public async Task<int> ContarTodasAsync(Guid envioId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SolicitudesReprogramacion.CountAsync(s => s.EnvioId == envioId, cancellationToken);
    }

    public async Task<int> ContarAprobadasAsync(Guid envioId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SolicitudesReprogramacion.CountAsync(s => s.EnvioId == envioId && s.Estado == "Aprobada", cancellationToken);
    }
}

public class EfOperadorRepository : IOperadorRepository
{
    private readonly AppDbContext _dbContext;

    public EfOperadorRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Operador?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Operadores.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }
}
