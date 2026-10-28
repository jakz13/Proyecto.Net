using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Mensajeria;

namespace UltimaMilla.Infrastructure.Persistence;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;

    public EfUnitOfWork(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Patrón outbox: el evento se guarda en la misma transacción que el cambio de estado que lo origina,
        // de modo que no puede persistirse uno sin el otro.
        await EncolarCambiosDeEstadoEnOutboxAsync(cancellationToken);

        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
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
    }

    /// <summary>Agrega al outbox un mensaje por cada evento de envío nuevo que implique un cambio real de estado (UC-39).</summary>
    private async Task EncolarCambiosDeEstadoEnOutboxAsync(CancellationToken cancellationToken)
    {
        _dbContext.ChangeTracker.DetectChanges();

        var eventosNuevos = _dbContext.ChangeTracker.Entries<EventoEnvio>()
            .Where(e => e.State == EntityState.Added && e.Entity.EstadoAnterior != e.Entity.EstadoNuevo)
            .Select(e => e.Entity)
            .ToList();

        if (eventosNuevos.Count == 0)
            return;

        var envios = _dbContext.ChangeTracker.Entries<Envio>()
            .ToDictionary(e => e.Entity.Id, e => (e.Entity.OperadorId, e.Entity.ComercioOperadorId, e.Entity.CodigoSeguimientoPublico));

        var faltantes = eventosNuevos.Select(e => e.EnvioId).Where(id => !envios.ContainsKey(id)).Distinct().ToList();
        if (faltantes.Count > 0)
        {
            var cargados = await _dbContext.Envios
                .AsNoTracking()
                .Where(e => faltantes.Contains(e.Id))
                .Select(e => new { e.Id, e.OperadorId, e.ComercioOperadorId, e.CodigoSeguimientoPublico })
                .ToListAsync(cancellationToken);

            foreach (var envio in cargados)
                envios[envio.Id] = (envio.OperadorId, envio.ComercioOperadorId, envio.CodigoSeguimientoPublico);
        }

        // El identificador de traza W3C de la solicitud original permite seguir la operación hasta el worker
        var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

        foreach (var evento in eventosNuevos)
        {
            if (!envios.TryGetValue(evento.EnvioId, out var datos))
                continue;

            var publicado = new EventoEnvioPublicado(
                datos.OperadorId,
                evento.EnvioId,
                datos.ComercioOperadorId,
                datos.CodigoSeguimientoPublico,
                evento.TipoEvento,
                evento.EstadoAnterior?.ToString(),
                evento.EstadoNuevo.ToString(),
                evento.Timestamp,
                correlationId);

            await _dbContext.OutboxMessages.AddAsync(
                new OutboxMessage(Guid.NewGuid(), nameof(Envio), evento.EnvioId, EventoEnvioPublicado.TipoMensaje, publicado.Serializar()),
                cancellationToken);
        }
    }
}
