using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Services;

/// <summary>
/// Banderas de funcionalidad por operador. Se leen del caché compartido (30 s de expiración como cota de propagación) y se persisten
/// en la base, con el historial de quién cambió qué y cuándo. Un cambio invalida la entrada compartida, así que llega de inmediato
/// a todas las instancias de la API y al worker sin reiniciarlos.
/// </summary>
public class FuncionalidadesService : IFuncionalidadesService
{
    private readonly AppDbContext _db;
    private readonly IDistributedCacheService _cache;
    private readonly IClock _clock;

    public FuncionalidadesService(AppDbContext db, IDistributedCacheService cache, IClock clock)
    {
        _db = db;
        _cache = cache;
        _clock = clock;
    }

    private static string Clave(Guid operadorId) => $"funcionalidades:operador:{operadorId:N}";

    public async Task<bool> EstaHabilitadaAsync(Guid operadorId, string clave, CancellationToken cancellationToken = default)
    {
        var valores = await ObtenerDelOperadorAsync(operadorId, cancellationToken);
        return !valores.TryGetValue(clave, out var habilitada) || habilitada;
    }

    public async Task<IReadOnlyDictionary<string, bool>> ObtenerDelOperadorAsync(Guid operadorId, CancellationToken cancellationToken = default)
    {
        var enCache = await _cache.GetAsync<Dictionary<string, bool>>(Clave(operadorId), cancellationToken);
        if (enCache is not null)
            return enCache;

        // Sin registro, la funcionalidad está habilitada
        var valores = Funcionalidad.Catalogo.Keys.ToDictionary(k => k, _ => true, StringComparer.Ordinal);
        var filas = await _db.BanderasFuncionalidad.AsNoTracking().Where(b => b.OperadorId == operadorId).ToListAsync(cancellationToken);
        foreach (var fila in filas.Where(f => valores.ContainsKey(f.Clave)))
            valores[fila.Clave] = fila.Habilitada;

        await _cache.SetAsync(Clave(operadorId), valores, IFuncionalidadesService.TiempoMaximoDePropagacion, cancellationToken);
        return valores;
    }

    public async Task<bool> EstablecerAsync(
        Guid operadorId, string clave, bool habilitada, Guid? usuarioId, string? usuarioNombre, CancellationToken cancellationToken = default)
    {
        if (!Funcionalidad.Existe(clave))
            throw new DomainValidationException($"La funcionalidad '{clave}' no existe.");

        if (!await _db.Operadores.IgnoreQueryFilters().AnyAsync(o => o.Id == operadorId, cancellationToken))
            throw new DomainValidationException("El operador indicado no existe.");

        var ahora = _clock.UtcNow;
        var bandera = await _db.BanderasFuncionalidad.FirstOrDefaultAsync(b => b.OperadorId == operadorId && b.Clave == clave, cancellationToken);

        if (bandera is null)
        {
            // Sin registro estaba habilitada: se crea con ese valor para que el cambio quede en el historial
            bandera = new BanderaFuncionalidad(Guid.NewGuid(), operadorId, clave, true, usuarioId, usuarioNombre, ahora);
            await _db.BanderasFuncionalidad.AddAsync(bandera, cancellationToken);
        }

        var cambio = bandera.Establecer(habilitada, usuarioId, usuarioNombre, ahora);
        if (cambio is null)
            return false;

        await _db.HistorialBanderas.AddAsync(cambio, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        // El cambio llega a todas las instancias: se invalida la entrada compartida
        await _cache.RemoveAsync(Clave(operadorId), cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<HistorialBandera>> HistorialAsync(Guid operadorId, CancellationToken cancellationToken = default) =>
        await _db.HistorialBanderas.AsNoTracking()
            .Where(h => h.OperadorId == operadorId)
            .OrderByDescending(h => h.Fecha)
            .Take(200)
            .ToListAsync(cancellationToken);
}
