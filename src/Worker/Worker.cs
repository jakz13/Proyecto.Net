namespace UltimaMilla.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UltimaMilla.Worker iniciado y en ejecución.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker latido activo a las: {time}", DateTimeOffset.Now);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
