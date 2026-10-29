using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Incidencias;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

public class EfOutboxRepository : IOutboxRepository
{
    private readonly AppDbContext _dbContext;

    public EfOutboxRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public Task<List<OutboxMessage>> ListarPendientesAsync(int maximo, CancellationToken cancellationToken = default) =>
        _dbContext.OutboxMessages
            .Where(m => !m.Procesado)
            .OrderBy(m => m.CreadoEn)
            .Take(maximo)
            .ToListAsync(cancellationToken);
}

public class EfIdempotenciaConsumidorRepository : IIdempotenciaConsumidorRepository
{
    private readonly AppDbContext _dbContext;

    public EfIdempotenciaConsumidorRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public Task<bool> ExisteAsync(string mensajeId, CancellationToken cancellationToken = default) =>
        _dbContext.ClavesIdempotenciaConsumidor.AnyAsync(c => c.MensajeId == mensajeId, cancellationToken);

    public async Task AddAsync(ClaveIdempotenciaConsumidor clave, CancellationToken cancellationToken = default) =>
        await _dbContext.ClavesIdempotenciaConsumidor.AddAsync(clave, cancellationToken);
}

public class EfMensajeFallidoRepository : IMensajeFallidoRepository
{
    private readonly AppDbContext _dbContext;

    public EfMensajeFallidoRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public Task<MensajeFallidoCola?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.MensajesFallidos.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task AddAsync(MensajeFallidoCola mensaje, CancellationToken cancellationToken = default) =>
        await _dbContext.MensajesFallidos.AddAsync(mensaje, cancellationToken);
}

public class EfNotificacionRepository : INotificacionRepository
{
    private readonly AppDbContext _dbContext;

    public EfNotificacionRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(Notificacion notificacion, CancellationToken cancellationToken = default) =>
        await _dbContext.Notificaciones.AddAsync(notificacion, cancellationToken);
}

public class EfAvisoComercioRepository : IAvisoComercioRepository
{
    private readonly AppDbContext _dbContext;

    public EfAvisoComercioRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public Task<List<SuscripcionAviso>> ListarSuscripcionesActivasAsync(Guid comercioOperadorId, CancellationToken cancellationToken = default) =>
        _dbContext.SuscripcionesAviso
            .AsNoTracking()
            .Where(s => s.ComercioOperadorId == comercioOperadorId && s.Activa)
            .ToListAsync(cancellationToken);

    public Task<SuscripcionAviso?> GetSuscripcionAsync(Guid suscripcionId, CancellationToken cancellationToken = default) =>
        suscripcionId == Guid.Empty
            ? Task.FromResult<SuscripcionAviso?>(null)
            : _dbContext.SuscripcionesAviso.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(s => s.Id == suscripcionId, cancellationToken);

    public async Task AddAsync(AvisoComercio aviso, CancellationToken cancellationToken = default) =>
        await _dbContext.AvisosComercio.AddAsync(aviso, cancellationToken);

    public Task<List<AvisoComercio>> ListarPendientesDeEntregaAsync(DateTime ahoraUtc, int maximo, CancellationToken cancellationToken = default) =>
        _dbContext.AvisosComercio
            .Where(a => (a.Estado == "Pendiente" || a.Estado == "Reintentando") && (a.ProximoReintento == null || a.ProximoReintento <= ahoraUtc))
            .OrderBy(a => a.Timestamp)
            .Take(maximo)
            .ToListAsync(cancellationToken);
}

public class EfIndicadorCumplimientoRepository : IIndicadorCumplimientoRepository
{
    private readonly AppDbContext _dbContext;

    public EfIndicadorCumplimientoRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public Task<List<IndicadorCumplimiento>> ListarPorPeriodoAsync(Guid operadorId, string periodo, CancellationToken cancellationToken = default) =>
        _dbContext.IndicadoresCumplimiento
            .Where(i => i.OperadorId == operadorId && i.Periodo == periodo)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(IndicadorCumplimiento indicador, CancellationToken cancellationToken = default) =>
        await _dbContext.IndicadoresCumplimiento.AddAsync(indicador, cancellationToken);
}

public class EfCierreDiarioRepository : ICierreDiarioRepository
{
    private readonly AppDbContext _dbContext;

    public EfCierreDiarioRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public Task<CierreDiarioOperacion?> GetAsync(Guid operadorId, DateOnly fecha, CancellationToken cancellationToken = default) =>
        _dbContext.CierresDiariosOperacion.FirstOrDefaultAsync(c => c.OperadorId == operadorId && c.Fecha == fecha, cancellationToken);

    public async Task AddAsync(CierreDiarioOperacion cierre, CancellationToken cancellationToken = default) =>
        await _dbContext.CierresDiariosOperacion.AddAsync(cierre, cancellationToken);
}

public class EfProcesosProgramadosRepository : IProcesosProgramadosRepository
{
    private readonly AppDbContext _dbContext;

    public EfProcesosProgramadosRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public Task<List<Guid>> ListarOperadoresActivosAsync(CancellationToken cancellationToken = default) =>
        _dbContext.Operadores.AsNoTracking().Where(o => o.Estado == "Activo").Select(o => o.Id).ToListAsync(cancellationToken);

    public Task<List<Envio>> ListarEnviosConResultadoAsync(Guid operadorId, DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken = default) =>
        _dbContext.Envios
            .AsNoTracking()
            .Where(e => e.OperadorId == operadorId
                        && e.Eventos.Any(ev => (ev.EstadoNuevo == EstadoEnvio.Entregado || ev.EstadoNuevo == EstadoEnvio.NoEntregado)
                                               && ev.Timestamp >= desdeUtc && ev.Timestamp < hastaUtcExclusivo))
            .ToListAsync(cancellationToken);

    public Task<int> ContarEnviosAdmitidosAsync(Guid operadorId, DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken = default) =>
        _dbContext.Envios.CountAsync(e => e.OperadorId == operadorId && e.FechaAlta >= desdeUtc && e.FechaAlta < hastaUtcExclusivo, cancellationToken);

    public async Task<(int Abiertas, int SinCerrar)> ContarIncidenciasAsync(Guid operadorId, DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken = default)
    {
        var abiertas = await _dbContext.Incidencias.CountAsync(
            i => i.OperadorId == operadorId && i.FechaApertura >= desdeUtc && i.FechaApertura < hastaUtcExclusivo, cancellationToken);

        var sinCerrar = await _dbContext.Incidencias.CountAsync(
            i => i.OperadorId == operadorId && i.FechaApertura < hastaUtcExclusivo && (i.FechaCierre == null || i.FechaCierre >= hastaUtcExclusivo), cancellationToken);

        return (abiertas, sinCerrar);
    }
}
