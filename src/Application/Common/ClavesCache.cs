using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace UltimaMilla.Application.Common;

/// <summary>
/// Centraliza la construcción de claves, tiempos de expiración y nombres de casos de uso del caché distribuido.
/// Todas las claves de configuración y operación distinguen explícitamente al operador logístico para impedir filtraciones entre inquilinos.
/// </summary>
public static class ClavesCache
{
    public const string CasoUsoSeguimientoPublico = "seguimiento_publico";
    public const string CasoUsoCuadroTarifario = "cuadro_tarifario";
    public const string CasoUsoIdentidadVisual = "identidad_visual";
    public const string CasoUsoGeneral = "general";

    /// <summary>
    /// Expiración para la identidad visual del operador en caché distribuido; se invalida inmediatamente por API y Worker al actualizarse.
    /// </summary>
    public static readonly TimeSpan ExpiracionIdentidadVisual = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Expiración corta (5 minutos) para el seguimiento público: recibe un alto volumen de lecturas, pero el estado
    /// del envío cambia varias veces al día y además se invalida al persistir o procesar cualquier evento del envío.
    /// </summary>
    public static readonly TimeSpan ExpiracionSeguimientoPublico = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Expiración media-larga (30 minutos) para el cuadro tarifario vigente y el historial de versiones del operador:
    /// se consulta de forma intensiva pero cambia con muy baja frecuencia, invalidándose al publicar una nueva versión.
    /// </summary>
    public static readonly TimeSpan ExpiracionCuadroTarifarioVigente = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Expiración larga (2 horas) para una versión puntual del cuadro tarifario identificada por operador e ID.
    /// </summary>
    public static readonly TimeSpan ExpiracionCuadroTarifarioVersion = TimeSpan.FromHours(2);

    public static string SeguimientoPorCodigo(string codigoSeguimiento) =>
        $"seguimiento:{NormalizarCodigo(codigoSeguimiento)}";

    public static string SeguimientoPorOperadorYCodigo(Guid operadorId, string codigoSeguimiento) =>
        $"seguimiento:operador:{operadorId:N}:{NormalizarCodigo(codigoSeguimiento)}";

    public static string EnvioPorId(Guid envioId) =>
        $"envio:{envioId}";

    public static string CuadroTarifarioVigente(Guid operadorId) =>
        $"tarifas:operador:{operadorId:N}:vigente";

    public static string CuadroTarifarioPorId(Guid operadorId, Guid cuadroId) =>
        $"tarifas:operador:{operadorId:N}:id:{cuadroId:N}";

    public static string CuadroTarifarioHistorial(Guid operadorId) =>
        $"tarifas:operador:{operadorId:N}:historial";

    public static string IdentidadVisualPorOperador(Guid operadorId) =>
        $"identidad-visual:operador:{operadorId:N}";

    public static string DeterminarCasoDeUso(string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave))
            return CasoUsoGeneral;

        if (clave.StartsWith("seguimiento:", StringComparison.OrdinalIgnoreCase)
            || clave.StartsWith("envio:", StringComparison.OrdinalIgnoreCase))
        {
            return CasoUsoSeguimientoPublico;
        }

        if (clave.StartsWith("tarifas:", StringComparison.OrdinalIgnoreCase))
        {
            return CasoUsoCuadroTarifario;
        }

        if (clave.StartsWith("identidad-visual:", StringComparison.OrdinalIgnoreCase))
        {
            return CasoUsoIdentidadVisual;
        }

        return CasoUsoGeneral;
    }

    private static string NormalizarCodigo(string codigoSeguimiento) =>
        (codigoSeguimiento ?? string.Empty).Trim().ToUpperInvariant();
}

/// <summary>
/// Instrumentación de métricas de aciertos, fallos, invalidaciones y errores de caché por caso de uso.
/// Emite contadores compatibles con OpenTelemetry (<see cref="Meter"/>) y mantiene un estado consultable para el tablero técnico.
/// </summary>
public static class MedidorCache
{
    public const string NombreMeter = "UltimaMilla.Cache";

    private static readonly Meter Meter = new(NombreMeter, "1.0.0");
    private static readonly Counter<long> ContadorAciertos = Meter.CreateCounter<long>(
        "ultimamilla_cache_hits_total",
        unit: "{hit}",
        description: "Cantidad total de aciertos en el caché distribuido por caso de uso.");
    private static readonly Counter<long> ContadorFallos = Meter.CreateCounter<long>(
        "ultimamilla_cache_misses_total",
        unit: "{miss}",
        description: "Cantidad total de fallos en el caché distribuido por caso de uso.");
    private static readonly Counter<long> ContadorInvalidaciones = Meter.CreateCounter<long>(
        "ultimamilla_cache_invalidations_total",
        unit: "{invalidation}",
        description: "Cantidad total de entradas invalidadas en el caché distribuido por caso de uso.");
    private static readonly Counter<long> ContadorErrores = Meter.CreateCounter<long>(
        "ultimamilla_cache_errors_total",
        unit: "{error}",
        description: "Cantidad total de fallas de comunicación con el almacén de caché distribuido.");

    private static readonly ConcurrentDictionary<string, ContadoresCasoUso> ContadoresPorCasoUso = new(StringComparer.OrdinalIgnoreCase);

    static MedidorCache()
    {
        InicializarCasosDeUsoConocidos();
    }

    public static void RegistrarAcierto(string? claveOCasoUso)
    {
        var casoUso = NormalizarCasoDeUso(claveOCasoUso);
        var contadores = ContadoresPorCasoUso.GetOrAdd(casoUso, _ => new ContadoresCasoUso());
        Interlocked.Increment(ref contadores.Aciertos);
        ContadorAciertos.Add(1, new KeyValuePair<string, object?>("caso_uso", casoUso));
    }

    public static void RegistrarFallo(string? claveOCasoUso)
    {
        var casoUso = NormalizarCasoDeUso(claveOCasoUso);
        var contadores = ContadoresPorCasoUso.GetOrAdd(casoUso, _ => new ContadoresCasoUso());
        Interlocked.Increment(ref contadores.Fallos);
        ContadorFallos.Add(1, new KeyValuePair<string, object?>("caso_uso", casoUso));
    }

    public static void RegistrarInvalidacion(string? claveOCasoUso)
    {
        var casoUso = NormalizarCasoDeUso(claveOCasoUso);
        var contadores = ContadoresPorCasoUso.GetOrAdd(casoUso, _ => new ContadoresCasoUso());
        Interlocked.Increment(ref contadores.Invalidaciones);
        ContadorInvalidaciones.Add(1, new KeyValuePair<string, object?>("caso_uso", casoUso));
    }

    public static void RegistrarError(string? claveOCasoUso)
    {
        var casoUso = NormalizarCasoDeUso(claveOCasoUso);
        var contadores = ContadoresPorCasoUso.GetOrAdd(casoUso, _ => new ContadoresCasoUso());
        Interlocked.Increment(ref contadores.Errores);
        ContadorErrores.Add(1, new KeyValuePair<string, object?>("caso_uso", casoUso));
    }

    public static ResumenMetricasCacheDto ObtenerResumen(string proveedorActivo = "Redis")
    {
        InicializarCasosDeUsoConocidos();

        var desglose = ContadoresPorCasoUso
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv =>
            {
                var aciertos = Interlocked.Read(ref kv.Value.Aciertos);
                var fallos = Interlocked.Read(ref kv.Value.Fallos);
                var invalidaciones = Interlocked.Read(ref kv.Value.Invalidaciones);
                var errores = Interlocked.Read(ref kv.Value.Errores);
                var totalConsultas = aciertos + fallos;
                var tasaAciertos = totalConsultas > 0
                    ? Math.Round((double)aciertos / totalConsultas * 100.0, 2)
                    : 0.0;

                return new MetricaCasoUsoCacheDto(
                    kv.Key,
                    aciertos,
                    fallos,
                    totalConsultas,
                    tasaAciertos,
                    invalidaciones,
                    errores);
            })
            .ToList();

        var totalAciertos = desglose.Sum(d => d.Aciertos);
        var totalFallos = desglose.Sum(d => d.Fallos);
        var totalConsultasGlobal = totalAciertos + totalFallos;
        var tasaGlobal = totalConsultasGlobal > 0
            ? Math.Round((double)totalAciertos / totalConsultasGlobal * 100.0, 2)
            : 0.0;

        return new ResumenMetricasCacheDto(
            proveedorActivo,
            totalAciertos,
            totalFallos,
            totalConsultasGlobal,
            tasaGlobal,
            desglose.Sum(d => d.Invalidaciones),
            desglose.Sum(d => d.ErroresDeConexion),
            desglose);
    }

    public static void ReiniciarParaPruebas()
    {
        ContadoresPorCasoUso.Clear();
        InicializarCasosDeUsoConocidos();
    }

    private static void InicializarCasosDeUsoConocidos()
    {
        ContadoresPorCasoUso.TryAdd(ClavesCache.CasoUsoSeguimientoPublico, new ContadoresCasoUso());
        ContadoresPorCasoUso.TryAdd(ClavesCache.CasoUsoCuadroTarifario, new ContadoresCasoUso());
    }

    private static string NormalizarCasoDeUso(string? claveOCasoUso)
    {
        if (string.IsNullOrWhiteSpace(claveOCasoUso))
            return ClavesCache.CasoUsoGeneral;

        if (string.Equals(claveOCasoUso, ClavesCache.CasoUsoSeguimientoPublico, StringComparison.OrdinalIgnoreCase))
            return ClavesCache.CasoUsoSeguimientoPublico;

        if (string.Equals(claveOCasoUso, ClavesCache.CasoUsoCuadroTarifario, StringComparison.OrdinalIgnoreCase))
            return ClavesCache.CasoUsoCuadroTarifario;

        return ClavesCache.DeterminarCasoDeUso(claveOCasoUso);
    }

    private sealed class ContadoresCasoUso
    {
        public long Aciertos;
        public long Fallos;
        public long Invalidaciones;
        public long Errores;
    }
}

public record MetricaCasoUsoCacheDto(
    string CasoDeUso,
    long Aciertos,
    long Fallos,
    long TotalConsultas,
    double TasaAciertosPorcentaje,
    long Invalidaciones,
    long ErroresDeConexion);

public record ResumenMetricasCacheDto(
    string Proveedor,
    long TotalAciertos,
    long TotalFallos,
    long TotalConsultas,
    double TasaAciertosPorcentaje,
    long TotalInvalidaciones,
    long TotalErroresDeConexion,
    IReadOnlyList<MetricaCasoUsoCacheDto> PorCasoDeUso);

