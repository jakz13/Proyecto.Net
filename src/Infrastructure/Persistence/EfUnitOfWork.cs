using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Retiros;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Infrastructure.Services;

namespace UltimaMilla.Infrastructure.Persistence;

public class EfUnitOfWork : IUnitOfWork
{
    private static readonly HashSet<EstadoEnvio> EstadosNotificablesRespaldo = new()
    {
        EstadoEnvio.Admitido,
        EstadoEnvio.AsignadoARuta,
        EstadoEnvio.EnTransito,
        EstadoEnvio.Entregado,
        EstadoEnvio.EntregadoParcialmente,
        EstadoEnvio.NoEntregado,
        EstadoEnvio.Reprogramado
    };

    private readonly AppDbContext _dbContext;
    private readonly IDistributedCacheService? _cacheService;
    private readonly IServiceScopeFactory? _scopeFactory;

    public EfUnitOfWork(
        AppDbContext dbContext,
        IDistributedCacheService? cacheService = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
        _scopeFactory = scopeFactory;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Patrón outbox: el evento se guarda en la misma transacción que el cambio de estado que lo origina,
        // de modo que no puede persistirse uno sin el otro.
        var (enviosAfectados, cuadrosAfectados, outboxNotificaciones) = await EncolarCambiosDeEstadoYDetectarEntidadesCacheadasAsync(cancellationToken);
        var codigosRetirosAfectados = await EncolarCambiosDeRetiroAsync(cancellationToken);

        int filasAfectadas;
        try
        {
            filasAfectadas = await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictoDeConcurrenciaException(
                "Los datos fueron modificados por otra operación simultánea. Recargue la información e intente nuevamente.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictoDeConcurrenciaException(
                "Otra operación simultánea ya registró los mismos datos (por ejemplo, el envío ya fue asignado a otra hoja de ruta).", ex);
        }

        // Una vez confirmada la transacción en base de datos, se invalidan las entradas correspondientes en el caché distribuido
        if (_cacheService is not null)
        {
            foreach (var (operadorId, codigo) in codigosRetirosAfectados)
            {
                await _cacheService.RemoveAsync(ClavesCache.SeguimientoPorCodigo(codigo), cancellationToken);
                await _cacheService.RemoveAsync(ClavesCache.SeguimientoPorOperadorYCodigo(operadorId, codigo), cancellationToken);
            }

            foreach (var envio in enviosAfectados)
            {
                await _cacheService.RemoveAsync(ClavesCache.SeguimientoPorCodigo(envio.CodigoSeguimientoPublico), cancellationToken);
                await _cacheService.RemoveAsync(ClavesCache.SeguimientoPorOperadorYCodigo(envio.OperadorId, envio.CodigoSeguimientoPublico), cancellationToken);
                await _cacheService.RemoveAsync(ClavesCache.EnvioPorId(envio.Id), cancellationToken);
            }

            foreach (var grupoOperador in cuadrosAfectados.GroupBy(c => c.OperadorId))
            {
                await _cacheService.RemoveAsync(ClavesCache.CuadroTarifarioVigente(grupoOperador.Key), cancellationToken);
                await _cacheService.RemoveAsync(ClavesCache.CuadroTarifarioHistorial(grupoOperador.Key), cancellationToken);
                foreach (var cuadro in grupoOperador)
                {
                    await _cacheService.RemoveAsync(ClavesCache.CuadroTarifarioPorId(cuadro.OperadorId, cuadro.Id), cancellationToken);
                }
            }
        }

        if (_scopeFactory is not null && outboxNotificaciones.Count > 0)
        {
            _ = DespacharRespaldoOutboxSiNoFueProcesadoAsync(outboxNotificaciones);
        }

        return filasAfectadas;
    }

    /// <summary>
    /// Respaldo para entornos locales donde el servicio Worker/RabbitMQ no está levantado:
    /// si pasados unos segundos el mensaje de Outbox sigue sin procesarse y SMTP está configurado,
    /// envía el correo al destinatario directamente sin duplicar envíos cuando el Worker sí está activo.
    /// </summary>
    private async Task DespacharRespaldoOutboxSiNoFueProcesadoAsync(
        List<(Guid OutboxId, Guid EnvioId, EstadoEnvio EstadoNuevo, string TipoEvento)> notificacionesPendientes)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(6));

            if (_scopeFactory is null)
                return;

            using var scope = _scopeFactory.CreateScope();
            var opciones = scope.ServiceProvider.GetService<IOptions<NotificacionesCorreoOptions>>()?.Value;
            if (opciones is null || !opciones.TieneCredencialesConfiguradas)
                return;

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var servicioNotificacion = scope.ServiceProvider.GetService<INotificacionDestinatarioService>();
            if (servicioNotificacion is null)
                return;

            foreach (var item in notificacionesPendientes)
            {
                if (!EstadosNotificablesRespaldo.Contains(item.EstadoNuevo))
                    continue;

                var outbox = await db.OutboxMessages
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == item.OutboxId);

                if (outbox is null || outbox.Procesado)
                    continue;

                var envio = await db.Envios
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == item.EnvioId);

                if (envio is null)
                    continue;

                var destinatario = await db.Destinatarios
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == envio.DestinatarioId);

                if (destinatario is null || string.IsNullOrWhiteSpace(destinatario.Email))
                    continue;

                await servicioNotificacion.EnviarAsync(
                    new NotificacionDestinatario(
                        envio.Id,
                        envio.CodigoSeguimientoPublico,
                        destinatario.Nombre,
                        "Email",
                        destinatario.Email,
                        item.EstadoNuevo));
            }
        }
        catch
        {
            // El respaldo en segundo plano no debe interrumpir el ciclo de vida de la aplicación
        }
    }

    /// <summary>
    /// Agrega al outbox un mensaje por cada evento de retiro nuevo: el cambio de estado y su evento se confirman en la misma
    /// transacción, igual que con los envíos, de modo que no puede persistirse uno sin el otro. Devuelve los retiros afectados
    /// para invalidar su caché de seguimiento público una vez confirmada la transacción.
    /// </summary>
    private async Task<List<(Guid OperadorId, string Codigo)>> EncolarCambiosDeRetiroAsync(CancellationToken cancellationToken)
    {
        var eventosNuevos = _dbContext.ChangeTracker.Entries<EventoRetiro>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        if (eventosNuevos.Count == 0)
            return new List<(Guid, string)>();

        var retirosRastreados = _dbContext.ChangeTracker.Entries<Retiro>()
            .ToDictionary(e => e.Entity.Id, e => e.Entity);

        var faltantes = eventosNuevos.Select(e => e.RetiroId).Where(id => !retirosRastreados.ContainsKey(id)).Distinct().ToList();
        if (faltantes.Count > 0)
        {
            var cargados = await _dbContext.Retiros.AsNoTracking().Where(r => faltantes.Contains(r.Id)).ToListAsync(cancellationToken);
            foreach (var retiro in cargados)
                retirosRastreados[retiro.Id] = retiro;
        }

        var correlationId = ContextoCorrelacion.ObtenerCorrelationIdActual();
        var afectados = new List<(Guid, string)>();

        foreach (var evento in eventosNuevos)
        {
            if (!retirosRastreados.TryGetValue(evento.RetiroId, out var retiro))
                continue;

            var publicado = new EventoRetiroPublicado(
                retiro.OperadorId,
                retiro.Id,
                retiro.ComercioOperadorId,
                retiro.CodigoSeguimientoPublico,
                evento.TipoEvento,
                evento.EstadoAnterior?.ToString(),
                evento.EstadoNuevo.ToString(),
                evento.Timestamp,
                correlationId,
                retiro.Bultos.Select(b => b.Identificacion).ToList());

            await _dbContext.OutboxMessages.AddAsync(
                new OutboxMessage(Guid.NewGuid(), nameof(Retiro), retiro.Id, EventoRetiroPublicado.TipoMensaje, publicado.Serializar()),
                cancellationToken);

            afectados.Add((retiro.OperadorId, retiro.CodigoSeguimientoPublico));
        }

        return afectados.Distinct().ToList();
    }

    /// <summary>
    /// Agrega al outbox un mensaje por cada evento de envío nuevo que implique un cambio real de estado o entrega parcial
    /// y recopila los envíos y cuadros tarifarios modificados para invalidar su caché tras confirmar la transacción.
    /// </summary>
    private async Task<(
        List<(Guid Id, Guid OperadorId, string CodigoSeguimientoPublico)> Envios,
        List<(Guid Id, Guid OperadorId)> Cuadros,
        List<(Guid OutboxId, Guid EnvioId, EstadoEnvio EstadoNuevo, string TipoEvento)> OutboxNotificaciones)>
        EncolarCambiosDeEstadoYDetectarEntidadesCacheadasAsync(CancellationToken cancellationToken)
    {
        _dbContext.ChangeTracker.DetectChanges();
        var outboxNotificaciones = new List<(Guid OutboxId, Guid EnvioId, EstadoEnvio EstadoNuevo, string TipoEvento)>();

        var enviosEntidades = _dbContext.ChangeTracker.Entries<Envio>()
            .ToDictionary(e => e.Entity.Id, e => e.Entity);

        var enviosRastreados = _dbContext.ChangeTracker.Entries<Envio>()
            .ToDictionary(
                e => e.Entity.Id,
                e => (e.Entity.Id, e.Entity.OperadorId, e.Entity.ComercioOperadorId, e.Entity.CodigoSeguimientoPublico, e.State));

        var todosEventosNuevos = _dbContext.ChangeTracker.Entries<EventoEnvio>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        var faltantes = todosEventosNuevos.Select(e => e.EnvioId).Where(id => !enviosRastreados.ContainsKey(id)).Distinct().ToList();
        if (faltantes.Count > 0)
        {
            var cargados = await _dbContext.Envios
                .AsNoTracking()
                .Where(e => faltantes.Contains(e.Id))
                .Select(e => new { e.Id, e.OperadorId, e.ComercioOperadorId, e.CodigoSeguimientoPublico })
                .ToListAsync(cancellationToken);

            foreach (var envio in cargados)
                enviosRastreados[envio.Id] = (envio.Id, envio.OperadorId, envio.ComercioOperadorId, envio.CodigoSeguimientoPublico, EntityState.Modified);
        }

        var eventosCambioEstado = todosEventosNuevos
            .Where(e => e.EstadoAnterior != e.EstadoNuevo || e.TipoEvento == "EntregaParcial")
            .ToList();

        // Toma el identificador de correlación fijado por el middleware (X-Correlation-Id) o la traza actual
        var correlationId = ContextoCorrelacion.ObtenerCorrelationIdActual();

        if (eventosCambioEstado.Count > 0)
        {
            foreach (var evento in eventosCambioEstado)
            {
                if (!enviosRastreados.TryGetValue(evento.EnvioId, out var datos))
                    continue;

                IReadOnlyList<EventoBultoPublicado>? bultosPublicados = null;
                if (enviosEntidades.TryGetValue(evento.EnvioId, out var entidadEnvio) && entidadEnvio.Bultos.Count > 0)
                {
                    bultosPublicados = entidadEnvio.Bultos
                        .Select(b => new EventoBultoPublicado(b.Identificacion, b.EstadoBulto, b.MotivoNoEntregaCodigo))
                        .ToList();
                }

                var publicado = new EventoEnvioPublicado(
                    datos.OperadorId,
                    evento.EnvioId,
                    datos.ComercioOperadorId,
                    datos.CodigoSeguimientoPublico,
                    evento.TipoEvento,
                    evento.EstadoAnterior?.ToString(),
                    evento.EstadoNuevo.ToString(),
                    evento.Timestamp,
                    correlationId,
                    bultosPublicados);

                var outboxId = Guid.NewGuid();
                await _dbContext.OutboxMessages.AddAsync(
                    new OutboxMessage(outboxId, nameof(Envio), evento.EnvioId, EventoEnvioPublicado.TipoMensaje, publicado.Serializar()),
                    cancellationToken);

                outboxNotificaciones.Add((outboxId, evento.EnvioId, evento.EstadoNuevo, evento.TipoEvento));
            }
        }

        var idsEnviosConEventos = todosEventosNuevos.Select(e => e.EnvioId).ToHashSet();
        var enviosAfectados = enviosRastreados.Values
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted || idsEnviosConEventos.Contains(e.Id))
            .Select(e => (e.Id, e.OperadorId, e.CodigoSeguimientoPublico))
            .Distinct()
            .ToList();

        var cuadrosAfectados = _dbContext.ChangeTracker.Entries<CuadroTarifario>()
            .Where(c => c.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(c => (c.Entity.Id, c.Entity.OperadorId))
            .Distinct()
            .ToList();

        return (enviosAfectados, cuadrosAfectados, outboxNotificaciones);
    }
}
