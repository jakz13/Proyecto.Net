using Microsoft.Extensions.Logging;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.Local;
using UltimaMilla.Mobile.Services.Sync;

namespace UltimaMilla.Mobile.Services.Tracking;

public record EstadoGeocercaParada(
    Guid ParadaId,
    Guid EnvioId,
    string CodigoSeguimiento,
    int Orden,
    double DistanciaMetros,
    bool DentroDeGeocerca,
    DateTime? TimestampEntradaUtc,
    int? UltimoTiempoPermanenciaSegundos
);

public class EventoGeocercaDisparadoEventArgs : EventArgs
{
    public required EventoGeocercaLocal Evento { get; init; }
    public required string DescripcionLegible { get; init; }
}

public class EvaluacionGeocercasResult
{
    public IReadOnlyList<EstadoGeocercaParada> EstadosPorParada { get; init; } = Array.Empty<EstadoGeocercaParada>();
    public IReadOnlyList<EventoGeocercaLocal> EventosDisparados { get; init; } = Array.Empty<EventoGeocercaLocal>();
    public double? DistanciaParadaMasCercanaMetros { get; init; }
    public string? CodigoParadaMasCercana { get; init; }
}

public interface IGeofenceService
{
    double RadioEntradaMetros { get; }
    double RadioSalidaMetros { get; }

    event EventHandler<EvaluacionGeocercasResult>? OnGeocercasEvaluadas;
    event EventHandler<EventoGeocercaDisparadoEventArgs>? OnEventoGeocercaDisparado;

    Task<EvaluacionGeocercasResult> EvaluarPosicionAsync(
        double latitudActual,
        double longitudActual,
        DateTime? timestampUtc = null,
        CancellationToken cancellationToken = default);

    bool EstaEnGeocerca(Guid paradaId);
    double? ObtenerUltimaDistanciaMetros(Guid paradaId);
    void ReiniciarEstadosMemoria();
}

/// <summary>
/// Motor de geocercas con histéresis (Entrada &lt; 100m, Salida &gt; 150m) usando fórmula de Haversine.
/// Persiste en SQLite cifrado y encola sincronización Offline-First hacia el backend (Sección 7.3).
/// </summary>
public class GeofenceService : IGeofenceService
{
    public const double UmbralEntradaMetros = 100.0;
    public const double UmbralSalidaMetros = 150.0;

    private readonly ILocalDatabaseService _localDatabaseService;
    private readonly ISyncService _syncService;
    private readonly ILogger<GeofenceService> _logger;
    private readonly SemaphoreSlim _evalLock = new(1, 1);

    private sealed class EstadoHisteresisParada
    {
        public bool DentroDeGeocerca { get; set; }
        public DateTime? TimestampEntradaUtc { get; set; }
        public double UltimaDistanciaMetros { get; set; }
        public int? UltimoTiempoPermanenciaSegundos { get; set; }
    }

    private readonly Dictionary<Guid, EstadoHisteresisParada> _estadosPorParada = new();

    public double RadioEntradaMetros => UmbralEntradaMetros;
    public double RadioSalidaMetros => UmbralSalidaMetros;

    public event EventHandler<EvaluacionGeocercasResult>? OnGeocercasEvaluadas;
    public event EventHandler<EventoGeocercaDisparadoEventArgs>? OnEventoGeocercaDisparado;

    public GeofenceService(
        ILocalDatabaseService localDatabaseService,
        ISyncService syncService,
        ILogger<GeofenceService> logger)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;
        _logger = logger;
    }

    public bool EstaEnGeocerca(Guid paradaId)
    {
        lock (_estadosPorParada)
        {
            return _estadosPorParada.TryGetValue(paradaId, out var estado) && estado.DentroDeGeocerca;
        }
    }

    public double? ObtenerUltimaDistanciaMetros(Guid paradaId)
    {
        lock (_estadosPorParada)
        {
            return _estadosPorParada.TryGetValue(paradaId, out var estado) ? estado.UltimaDistanciaMetros : null;
        }
    }

    public void ReiniciarEstadosMemoria()
    {
        lock (_estadosPorParada)
        {
            _estadosPorParada.Clear();
        }
    }

    public async Task<EvaluacionGeocercasResult> EvaluarPosicionAsync(
        double latitudActual,
        double longitudActual,
        DateTime? timestampUtc = null,
        CancellationToken cancellationToken = default)
    {
        await _evalLock.WaitAsync(cancellationToken);
        try
        {
            var ahoraUtc = timestampUtc ?? DateTime.UtcNow;
            var rutaLocal = await _localDatabaseService.ObtenerRutaActivaLocalAsync();
            if (rutaLocal == null || rutaLocal.Paradas.Count == 0)
            {
                await _localDatabaseService.SeedRutaDemoSiEstaVaciaAsync();
                rutaLocal = await _localDatabaseService.ObtenerRutaActivaLocalAsync();
            }

            if (rutaLocal == null || rutaLocal.Paradas.Count == 0)
            {
                return new EvaluacionGeocercasResult();
            }

            var ubicacionRepartidor = new Location(latitudActual, longitudActual);
            var estadosResultado = new List<EstadoGeocercaParada>();
            var eventosDisparados = new List<EventoGeocercaLocal>();

            double? menorDistanciaPendiente = null;
            string? codigoParadaMasCercana = null;

            foreach (var parada in rutaLocal.Paradas.OrderBy(p => p.Orden))
            {
                var (latParada, lonParada) = (parada.Latitud != 0 && parada.Longitud != 0)
                    ? (parada.Latitud, parada.Longitud)
                    : DemoDataDefaults.ObtenerCoordenadasPorParada(parada.CodigoSeguimiento, parada.Orden);

                var ubicacionParada = new Location(latParada, lonParada);

                // Cálculo exacto en metros con fórmula de Haversine (Location.CalculateDistance)
                double distanciaKm = Location.CalculateDistance(
                    ubicacionRepartidor,
                    ubicacionParada,
                    DistanceUnits.Kilometers);
                double distanciaMetros = Math.Round(distanciaKm * 1000.0, 1);

                bool esPendiente = !string.Equals(parada.Estado, "Entregado", StringComparison.OrdinalIgnoreCase);
                if (esPendiente && (!menorDistanciaPendiente.HasValue || distanciaMetros < menorDistanciaPendiente.Value))
                {
                    menorDistanciaPendiente = distanciaMetros;
                    codigoParadaMasCercana = parada.CodigoSeguimiento;
                }

                EstadoHisteresisParada estadoMemoria;
                lock (_estadosPorParada)
                {
                    if (!_estadosPorParada.TryGetValue(parada.Id, out estadoMemoria!))
                    {
                        estadoMemoria = new EstadoHisteresisParada();
                        _estadosPorParada[parada.Id] = estadoMemoria;
                    }
                    estadoMemoria.UltimaDistanciaMetros = distanciaMetros;
                }

                // Histéresis:
                // 1) Radio de Entrada (< 100m): solo dispara si NO estaba dentro de la geocerca
                if (!estadoMemoria.DentroDeGeocerca && distanciaMetros < UmbralEntradaMetros)
                {
                    estadoMemoria.DentroDeGeocerca = true;
                    estadoMemoria.TimestampEntradaUtc = ahoraUtc;

                    var eventoEntrada = new EventoGeocercaLocal
                    {
                        HojaDeRutaId = parada.HojaDeRutaId != Guid.Empty ? parada.HojaDeRutaId : DemoDataDefaults.DefaultHojaDeRutaId,
                        ParadaId = parada.Id,
                        EnvioId = parada.EnvioId,
                        CodigoSeguimiento = parada.CodigoSeguimiento,
                        TipoEvento = "LLEGADA_GEOCERCA",
                        Latitud = latitudActual,
                        Longitud = longitudActual,
                        DistanciaMetros = distanciaMetros,
                        TimestampUtc = ahoraUtc,
                        TiempoPermanenciaSegundos = null,
                        Sincronizado = false
                    };

                    await _localDatabaseService.RegistrarEventoGeocercaLocalAsync(eventoEntrada);
                    eventosDisparados.Add(eventoEntrada);

                    _logger.LogInformation(
                        "LLEGADA_GEOCERCA detectada en Parada {Orden} ({Codigo}) a {Distancia}m.",
                        parada.Orden, parada.CodigoSeguimiento, distanciaMetros);

                    OnEventoGeocercaDisparado?.Invoke(this, new EventoGeocercaDisparadoEventArgs
                    {
                        Evento = eventoEntrada,
                        DescripcionLegible = $"Llegada a geocerca de Parada #{parada.Orden} ({parada.CodigoSeguimiento}) a {distanciaMetros:F0} m (< 100 m)."
                    });
                }
                // 2) Radio de Salida (> 150m): solo dispara si YA había registrado entrada en esa parada
                else if (estadoMemoria.DentroDeGeocerca && distanciaMetros > UmbralSalidaMetros)
                {
                    int permanenciaSegundos = estadoMemoria.TimestampEntradaUtc.HasValue
                        ? Math.Max(1, (int)Math.Round((ahoraUtc - estadoMemoria.TimestampEntradaUtc.Value).TotalSeconds))
                        : 1;

                    estadoMemoria.DentroDeGeocerca = false;
                    estadoMemoria.UltimoTiempoPermanenciaSegundos = permanenciaSegundos;
                    estadoMemoria.TimestampEntradaUtc = null;

                    var eventoSalida = new EventoGeocercaLocal
                    {
                        HojaDeRutaId = parada.HojaDeRutaId != Guid.Empty ? parada.HojaDeRutaId : DemoDataDefaults.DefaultHojaDeRutaId,
                        ParadaId = parada.Id,
                        EnvioId = parada.EnvioId,
                        CodigoSeguimiento = parada.CodigoSeguimiento,
                        TipoEvento = "SALIDA_GEOCERCA",
                        Latitud = latitudActual,
                        Longitud = longitudActual,
                        DistanciaMetros = distanciaMetros,
                        TimestampUtc = ahoraUtc,
                        TiempoPermanenciaSegundos = permanenciaSegundos,
                        Sincronizado = false
                    };

                    await _localDatabaseService.RegistrarEventoGeocercaLocalAsync(eventoSalida);
                    eventosDisparados.Add(eventoSalida);

                    _logger.LogInformation(
                        "SALIDA_GEOCERCA detectada en Parada {Orden} ({Codigo}) a {Distancia}m. Permanencia: {Segundos}s.",
                        parada.Orden, parada.CodigoSeguimiento, distanciaMetros, permanenciaSegundos);

                    OnEventoGeocercaDisparado?.Invoke(this, new EventoGeocercaDisparadoEventArgs
                    {
                        Evento = eventoSalida,
                        DescripcionLegible = $"Salida de geocerca de Parada #{parada.Orden} ({parada.CodigoSeguimiento}) a {distanciaMetros:F0} m (> 150 m). Permanencia: {permanenciaSegundos} s."
                    });
                }

                estadosResultado.Add(new EstadoGeocercaParada(
                    parada.Id,
                    parada.EnvioId,
                    parada.CodigoSeguimiento,
                    parada.Orden,
                    distanciaMetros,
                    estadoMemoria.DentroDeGeocerca,
                    estadoMemoria.TimestampEntradaUtc,
                    estadoMemoria.UltimoTiempoPermanenciaSegundos));
            }

            if (eventosDisparados.Count > 0 && _syncService.IsOnline)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _syncService.SincronizarOperacionesPendientesAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Los eventos de geocerca quedaron encolados en SQLite para sincronización posterior.");
                    }
                });
            }

            var resultado = new EvaluacionGeocercasResult
            {
                EstadosPorParada = estadosResultado,
                EventosDisparados = eventosDisparados,
                DistanciaParadaMasCercanaMetros = menorDistanciaPendiente ?? estadosResultado.Select(e => (double?)e.DistanciaMetros).Min(),
                CodigoParadaMasCercana = codigoParadaMasCercana ?? estadosResultado.FirstOrDefault()?.CodigoSeguimiento
            };

            OnGeocercasEvaluadas?.Invoke(this, resultado);
            return resultado;
        }
        finally
        {
            _evalLock.Release();
        }
    }
}

