using MediatR;
using Microsoft.Extensions.Options;
using UltimaMilla.Application.Commands.Mensajeria.PublicarOutboxPendiente;

namespace UltimaMilla.Worker.BackgroundServices;

/// <summary>
/// Lleva al broker los eventos que la API dejó en el outbox. Si el broker no está disponible los mensajes permanecen
/// sin procesar y se retoman en la vuelta siguiente: ninguno se pierde ni se publica fuera de orden.
/// </summary>
public class OutboxPublisherBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerOptions _opciones;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger;

    public OutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> opciones,
        ILogger<OutboxPublisherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _opciones = opciones.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Publicador del outbox iniciado (cada {Segundos}s, lote de {Lote}).", _opciones.OutboxIntervaloSegundos, _opciones.OutboxTamanoLote);

        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _opciones.OutboxIntervaloSegundos)));
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var resultado = await mediator.Send(new PublicarOutboxPendienteCommand(_opciones.OutboxTamanoLote), stoppingToken);

                if (resultado.Error is not null)
                    _logger.LogWarning("Broker no disponible: {Pendientes} mensaje(s) del outbox quedan pendientes. Motivo: {Motivo}", resultado.Pendientes, resultado.Error);
                else if (resultado.Publicados > 0)
                    _logger.LogInformation("Outbox: {Publicados} mensaje(s) publicados.", resultado.Publicados);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la vuelta de publicación del outbox; se reintenta en la próxima.");
            }
        }
        while (await SiguienteVueltaAsync(temporizador, stoppingToken));
    }

    private static async Task<bool> SiguienteVueltaAsync(PeriodicTimer temporizador, CancellationToken cancellationToken)
    {
        try
        {
            return await temporizador.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
