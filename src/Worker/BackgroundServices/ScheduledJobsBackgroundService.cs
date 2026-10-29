using MediatR;
using Microsoft.Extensions.Options;
using UltimaMilla.Application.Commands.Indicadores.EjecutarCierreDiario;
using UltimaMilla.Application.Commands.Indicadores.RecalcularIndicadoresCumplimiento;
using UltimaMilla.Application.Commands.Mensajeria.ReintentarAvisosPendientes;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Tablero;

namespace UltimaMilla.Worker.BackgroundServices;

/// <summary>
/// Tareas periódicas del worker: reintento de avisos a comercios cuyo plazo de espera venció (UC-36) y, pasada la hora de cierre,
/// recálculo de indicadores (UC-37) y cierre diario (UC-38) de cada operador activo. Cada operador se procesa en su propio alcance.
/// Si hay más de una instancia, la unicidad de los cierres y la idempotencia de los comandos evitan que un día se cierre dos veces.
/// </summary>
public class ScheduledJobsBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerOptions _opciones;
    private readonly ILogger<ScheduledJobsBackgroundService> _logger;
    private readonly TimeOnly _horaCierre;
    private DateOnly? _ultimaFechaProcesada;
    private bool _pendienteJornadaAnterior = true;

    public ScheduledJobsBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> opciones,
        ILogger<ScheduledJobsBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _opciones = opciones.Value;
        _logger = logger;
        _horaCierre = TimeOnly.TryParse(_opciones.CierreDiarioHoraLocal, out var hora) ? hora : new TimeOnly(23, 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Procesos programados iniciados (cierre diario a las {Hora} hora local).", _horaCierre);

        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _opciones.ProcesosProgramadosIntervaloSegundos)));
        do
        {
            await EjecutarSeguroAsync("reintento de avisos a comercios", ReintentarAvisosAsync, stoppingToken);
            await EjecutarSeguroAsync("cierre diario", EvaluarCierreDiarioAsync, stoppingToken);
        }
        while (await SiguienteVueltaAsync(temporizador, stoppingToken));
    }

    private async Task ReintentarAvisosAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var resultado = await mediator.Send(new ReintentarAvisosPendientesCommand(_opciones.AvisosTamanoLote), cancellationToken);

        if (resultado.Entregados + resultado.EnReintento + resultado.Agotados > 0)
            _logger.LogInformation(
                "Avisos a comercios: {Entregados} entregado(s), {EnReintento} en reintento, {Agotados} agotado(s).",
                resultado.Entregados, resultado.EnReintento, resultado.Agotados);
    }

    private async Task EvaluarCierreDiarioAsync(CancellationToken cancellationToken)
    {
        var ahoraLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ZonaHorariaOperacion.Zona);
        var hoy = DateOnly.FromDateTime(ahoraLocal);

        var fechas = new List<DateOnly>();
        if (_pendienteJornadaAnterior)
            fechas.Add(hoy.AddDays(-1)); // si el worker estuvo caído durante el cierre, se recupera la jornada anterior (idempotente)

        if (TimeOnly.FromDateTime(ahoraLocal) >= _horaCierre && _ultimaFechaProcesada != hoy)
            fechas.Add(hoy);

        if (fechas.Count == 0)
            return;

        List<Guid> operadores;
        using (var scope = _scopeFactory.CreateScope())
        {
            operadores = await scope.ServiceProvider.GetRequiredService<IProcesosProgramadosRepository>().ListarOperadoresActivosAsync(cancellationToken);
        }

        var huboFallas = false;
        foreach (var fecha in fechas)
        {
            foreach (var operadorId in operadores)
                huboFallas |= !await CerrarJornadaAsync(operadorId, fecha, cancellationToken);
        }

        if (!huboFallas)
        {
            _pendienteJornadaAnterior = false;
            if (fechas.Contains(hoy))
                _ultimaFechaProcesada = hoy;
        }
    }

    /// <returns><c>true</c> si la jornada del operador quedó cerrada (por esta instancia o por otra); <c>false</c> si falló y debe reintentarse.</returns>
    private async Task<bool> CerrarJornadaAsync(Guid operadorId, DateOnly fecha, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<ICurrentTenant>().EstablecerOperadorDeProceso(operadorId);
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var indicadores = await mediator.Send(new RecalcularIndicadoresCumplimientoCommand(operadorId, fecha), cancellationToken);
            var cierre = await mediator.Send(new EjecutarCierreDiarioCommand(operadorId, fecha), cancellationToken);

            if (!cierre.YaExistia)
                _logger.LogInformation(
                    "Cierre diario {Fecha} registrado para el operador {OperadorId}; {Total} resultado(s) de entrega consolidados.",
                    fecha, operadorId, indicadores.TotalEnvios);

            return true;
        }
        catch (ConflictoDeConcurrenciaException)
        {
            _logger.LogInformation("El cierre {Fecha} del operador {OperadorId} ya fue registrado por otra instancia.", fecha, operadorId);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el cierre {Fecha} del operador {OperadorId}; se reintenta en la próxima vuelta.", fecha, operadorId);
            return false;
        }
    }

    private async Task EjecutarSeguroAsync(string nombre, Func<CancellationToken, Task> tarea, CancellationToken cancellationToken)
    {
        try
        {
            await tarea(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la tarea programada de {Tarea}; se reintenta en la próxima vuelta.", nombre);
        }
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
