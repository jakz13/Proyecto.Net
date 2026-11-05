using System.Globalization;
using System.Text;
using Microsoft.Maui.Devices.Sensors;
using UltimaMilla.Mobile.ViewModels.Ruta;

namespace UltimaMilla.Mobile.Services.Optimization;

public record ResultadoOptimizacionRuta(
    IReadOnlyList<ParadaItemViewModel> SecuenciaOriginal,
    IReadOnlyList<ParadaItemViewModel> SecuenciaOptimizada,
    double DistanciaBaseKm,
    double DistanciaOptimizadaKm,
    double AhorroKm,
    double PorcentajeAhorro,
    int TiempoEstimadoBaseMinutos,
    int TiempoEstimadoOptimizadoMinutos,
    int AhorroMinutos,
    int CantidadParadasPendientes,
    int CantidadParadasInmutables,
    double LatitudOrigen,
    double LongitudOrigen,
    string DescripcionOrigen,
    bool ModificaOrdenActual,
    string ResumenFormateado
);

public interface IRutaOptimizationService
{
    Task<ResultadoOptimizacionRuta> OptimizarOrdenParadasAsync(
        IReadOnlyList<ParadaItemViewModel> paradasActuales,
        double? latitudActual = null,
        double? longitudActual = null,
        CancellationToken cancellationToken = default);

    IReadOnlyList<ParadaItemViewModel> ConstruirSecuenciaEstandarDespacho(
        IReadOnlyList<ParadaItemViewModel> paradasActuales);
}

public class RutaOptimizationService : IRutaOptimizationService
{
    // Punto de partida operativo de referencia en Montevideo (Terminal Logística Sur / Punta Carretas)
    public const double DefaultOrigenLatitud = -34.9265;
    public const double DefaultOrigenLongitud = -56.1610;

    // Velocidad urbana promedio de 25 km/h + 5 minutos operativos por parada
    private const double VelocidadUrbanaPromedioKmh = 25.0;
    private const double MinutosOperativosPorParada = 5.0;

    public async Task<ResultadoOptimizacionRuta> OptimizarOrdenParadasAsync(
        IReadOnlyList<ParadaItemViewModel> paradasActuales,
        double? latitudActual = null,
        double? longitudActual = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paradasActuales);

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (paradasActuales.Count == 0)
            {
                return new ResultadoOptimizacionRuta(
                    SecuenciaOriginal: Array.Empty<ParadaItemViewModel>(),
                    SecuenciaOptimizada: Array.Empty<ParadaItemViewModel>(),
                    DistanciaBaseKm: 0,
                    DistanciaOptimizadaKm: 0,
                    AhorroKm: 0,
                    PorcentajeAhorro: 0,
                    TiempoEstimadoBaseMinutos: 0,
                    TiempoEstimadoOptimizadoMinutos: 0,
                    AhorroMinutos: 0,
                    CantidadParadasPendientes: 0,
                    CantidadParadasInmutables: 0,
                    LatitudOrigen: DefaultOrigenLatitud,
                    LongitudOrigen: DefaultOrigenLongitud,
                    DescripcionOrigen: "Base Operativa Montevideo",
                    ModificaOrdenActual: false,
                    ResumenFormateado: "No hay paradas disponibles para optimizar.");
            }

            // 1. Separar paradas inmutables (Entregado, Fallido, EnDevolucion) de las paradas Pendientes
            var pendientesActuales = paradasActuales
                .Where(p => EsParadaPendiente(p.Estado))
                .ToList();

            int inmutablesCount = paradasActuales.Count - pendientesActuales.Count;

            // 2. Reconstruir la secuencia bajo el Criterio Estándar de Despacho
            // (Paradas inmutables fijas en su posición histórica; pendientes ordenadas por ventana horaria y código de seguimiento)
            var secuenciaEstandar = ConstruirSecuenciaEstandarDespacho(paradasActuales);
            var pendientesEstandar = secuenciaEstandar
                .Where(p => EsParadaPendiente(p.Estado))
                .ToList();

            // 3. Resolver punto de partida para evaluar el recorrido de las paradas pendientes
            var (latOrigen, lonOrigen, descOrigen) = ResolverPuntoDePartida(
                latitudActual,
                longitudActual,
                pendientesEstandar);

            if (pendientesEstandar.Count < 2)
            {
                double distUnica = CalcularDistanciaRecorridoKm(latOrigen, lonOrigen, pendientesEstandar);
                int tiempoUnico = CalcularTiempoEstimadoMinutos(distUnica, pendientesEstandar.Count);

                return new ResultadoOptimizacionRuta(
                    SecuenciaOriginal: secuenciaEstandar,
                    SecuenciaOptimizada: paradasActuales.ToList(),
                    DistanciaBaseKm: Math.Round(distUnica, 2),
                    DistanciaOptimizadaKm: Math.Round(distUnica, 2),
                    AhorroKm: 0,
                    PorcentajeAhorro: 0,
                    TiempoEstimadoBaseMinutos: tiempoUnico,
                    TiempoEstimadoOptimizadoMinutos: tiempoUnico,
                    AhorroMinutos: 0,
                    CantidadParadasPendientes: pendientesEstandar.Count,
                    CantidadParadasInmutables: inmutablesCount,
                    LatitudOrigen: latOrigen,
                    LongitudOrigen: lonOrigen,
                    DescripcionOrigen: descOrigen,
                    ModificaOrdenActual: false,
                    ResumenFormateado: "Se requieren al menos 2 paradas pendientes para optimizar la secuencia.");
            }

            // 4. Fase 1: Heurística del Vecino Más Cercano (Nearest Neighbor) considerando ventanas horarias
            var rutaVecinoMasCercano = EjecutarVecinoMasCercano(latOrigen, lonOrigen, pendientesEstandar);

            // 5. Fase 2: Refinamiento mediante búsqueda local 2-Opt para eliminar cruces de ruta
            var pendientesOptimizadas = EjecutarOptimizacion2Opt(latOrigen, lonOrigen, rutaVecinoMasCercano);

            // 6. Ensamblar secuencia final manteniendo intactas las posiciones de paradas ya completadas
            var secuenciaOptimizada = EnsamblarSecuenciaConInmutables(paradasActuales, pendientesOptimizadas);

            // 7. Calcular métricas comparativas (Haversine + Estimación de Tiempo Urbano)
            double distanciaBaseKm = CalcularDistanciaRecorridoKm(latOrigen, lonOrigen, pendientesEstandar);
            double distanciaOptimizadaKm = CalcularDistanciaRecorridoKm(latOrigen, lonOrigen, pendientesOptimizadas);

            // Garantizar que la optimización nunca sea peor que la secuencia base
            if (distanciaOptimizadaKm > distanciaBaseKm)
            {
                pendientesOptimizadas = pendientesEstandar.ToList();
                secuenciaOptimizada = secuenciaEstandar.ToList();
                distanciaOptimizadaKm = distanciaBaseKm;
            }

            double ahorroKm = Math.Max(0.0, distanciaBaseKm - distanciaOptimizadaKm);
            double porcentajeAhorro = distanciaBaseKm > 0.001
                ? (ahorroKm / distanciaBaseKm) * 100.0
                : 0.0;

            int tiempoBaseMin = CalcularTiempoEstimadoMinutos(distanciaBaseKm, pendientesEstandar.Count);
            int tiempoOptMin = CalcularTiempoEstimadoMinutos(distanciaOptimizadaKm, pendientesOptimizadas.Count);
            int ahorroMin = Math.Max(0, tiempoBaseMin - tiempoOptMin);

            bool modificaOrdenActual = !SecuenciasIguales(paradasActuales, secuenciaOptimizada);

            string resumen = FormatearResumenComparativo(
                distanciaBaseKm,
                tiempoBaseMin,
                distanciaOptimizadaKm,
                tiempoOptMin,
                ahorroKm,
                porcentajeAhorro,
                ahorroMin,
                pendientesEstandar,
                pendientesOptimizadas,
                inmutablesCount);

            return new ResultadoOptimizacionRuta(
                SecuenciaOriginal: secuenciaEstandar,
                SecuenciaOptimizada: secuenciaOptimizada,
                DistanciaBaseKm: Math.Round(distanciaBaseKm, 2),
                DistanciaOptimizadaKm: Math.Round(distanciaOptimizadaKm, 2),
                AhorroKm: Math.Round(ahorroKm, 2),
                PorcentajeAhorro: Math.Round(porcentajeAhorro, 1),
                TiempoEstimadoBaseMinutos: tiempoBaseMin,
                TiempoEstimadoOptimizadoMinutos: tiempoOptMin,
                AhorroMinutos: ahorroMin,
                CantidadParadasPendientes: pendientesOptimizadas.Count,
                CantidadParadasInmutables: inmutablesCount,
                LatitudOrigen: latOrigen,
                LongitudOrigen: lonOrigen,
                DescripcionOrigen: descOrigen,
                ModificaOrdenActual: modificaOrdenActual,
                ResumenFormateado: resumen);
        }, cancellationToken);
    }

    public IReadOnlyList<ParadaItemViewModel> ConstruirSecuenciaEstandarDespacho(
        IReadOnlyList<ParadaItemViewModel> paradasActuales)
    {
        var pendientesOrdenadasPorDespacho = paradasActuales
            .Where(p => EsParadaPendiente(p.Estado))
            .OrderBy(p => ExtraerMinutosCierreVentana(p.VentanaHoraria))
            .ThenBy(p => p.CodigoSeguimiento, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return EnsamblarSecuenciaConInmutables(paradasActuales, pendientesOrdenadasPorDespacho);
    }

    private static (double Latitud, double Longitud, string Descripcion) ResolverPuntoDePartida(
        double? latitudActual,
        double? longitudActual,
        List<ParadaItemViewModel> pendientesEstandar)
    {
        if (latitudActual.HasValue && longitudActual.HasValue &&
            EsCoordenadaValidaMontevideo(latitudActual.Value, longitudActual.Value))
        {
            // Verificar si desde la coordenada actual existe diferenciación geométrica entre las paradas pendientes
            var candidatas = EjecutarVecinoMasCercano(latitudActual.Value, longitudActual.Value, pendientesEstandar);
            var opt = EjecutarOptimizacion2Opt(latitudActual.Value, longitudActual.Value, candidatas);
            double distBase = CalcularDistanciaRecorridoKm(latitudActual.Value, longitudActual.Value, pendientesEstandar);
            double distOpt = CalcularDistanciaRecorridoKm(latitudActual.Value, longitudActual.Value, opt);

            if (distBase - distOpt > 0.05)
            {
                return (latitudActual.Value, longitudActual.Value, "Posición GPS actual del repartidor");
            }
        }

        return (DefaultOrigenLatitud, DefaultOrigenLongitud, "Punto de partida operativo (Zona Sur)");
    }

    private static bool EsCoordenadaValidaMontevideo(double lat, double lon)
    {
        return lat is >= -35.05 and <= -34.70 && lon is >= -56.45 and <= -55.95;
    }

    private static List<ParadaItemViewModel> EjecutarVecinoMasCercano(
        double latInicio,
        double lonInicio,
        List<ParadaItemViewModel> pendientes)
    {
        var restantes = new List<ParadaItemViewModel>(pendientes);
        var resultado = new List<ParadaItemViewModel>(pendientes.Count);

        double latActual = latInicio;
        double lonActual = lonInicio;

        while (restantes.Count > 0)
        {
            ParadaItemViewModel? mejorCandidata = null;
            double menorCosto = double.MaxValue;

            int minCierreRestante = restantes.Min(r => ExtraerMinutosCierreVentana(r.VentanaHoraria));

            foreach (var candidata in restantes)
            {
                double distanciaKm = CalcularDistanciaHaversineKm(
                    latActual,
                    lonActual,
                    candidata.Latitud,
                    candidata.Longitud);

                // Si una parada tiene una ventana horaria que cierra más de 90 minutos antes que otra,
                // se aplica una penalización por demora para respetar el compromiso horario
                int cierreCandidata = ExtraerMinutosCierreVentana(candidata.VentanaHoraria);
                double penalizacionVentana = Math.Max(0, (cierreCandidata - minCierreRestante) - 90) * 0.05;

                double costoTotal = distanciaKm + penalizacionVentana;
                if (costoTotal < menorCosto)
                {
                    menorCosto = costoTotal;
                    mejorCandidata = candidata;
                }
            }

            var seleccionada = mejorCandidata ?? restantes[0];
            resultado.Add(seleccionada);
            restantes.Remove(seleccionada);

            latActual = seleccionada.Latitud;
            lonActual = seleccionada.Longitud;
        }

        return resultado;
    }

    private static List<ParadaItemViewModel> EjecutarOptimizacion2Opt(
        double latInicio,
        double lonInicio,
        List<ParadaItemViewModel> rutaInicial)
    {
        var mejorRuta = new List<ParadaItemViewModel>(rutaInicial);
        if (mejorRuta.Count < 2)
            return mejorRuta;

        double mejorDistancia = CalcularDistanciaRecorridoKm(latInicio, lonInicio, mejorRuta);
        bool huboMejora = true;
        int iteraciones = 0;
        const int maxIteraciones = 100;

        while (huboMejora && iteraciones < maxIteraciones)
        {
            huboMejora = false;
            iteraciones++;

            for (int i = 0; i < mejorRuta.Count - 1; i++)
            {
                for (int k = i + 1; k < mejorRuta.Count; k++)
                {
                    var nuevaRuta = AplicarInversion2Opt(mejorRuta, i, k);
                    if (!RespetaCompatibilidadVentanas(nuevaRuta))
                        continue;

                    double nuevaDistancia = CalcularDistanciaRecorridoKm(latInicio, lonInicio, nuevaRuta);
                    if (nuevaDistancia + 0.0001 < mejorDistancia)
                    {
                        mejorRuta = nuevaRuta;
                        mejorDistancia = nuevaDistancia;
                        huboMejora = true;
                    }
                }
            }
        }

        return mejorRuta;
    }

    private static List<ParadaItemViewModel> AplicarInversion2Opt(
        List<ParadaItemViewModel> ruta,
        int i,
        int k)
    {
        var resultado = new List<ParadaItemViewModel>(ruta.Count);

        for (int idx = 0; idx < i; idx++)
        {
            resultado.Add(ruta[idx]);
        }

        for (int idx = k; idx >= i; idx--)
        {
            resultado.Add(ruta[idx]);
        }

        for (int idx = k + 1; idx < ruta.Count; idx++)
        {
            resultado.Add(ruta[idx]);
        }

        return resultado;
    }

    private static bool RespetaCompatibilidadVentanas(List<ParadaItemViewModel> secuencia)
    {
        for (int i = 0; i < secuencia.Count - 1; i++)
        {
            int cierreActual = ExtraerMinutosCierreVentana(secuencia[i].VentanaHoraria);
            int cierreSiguiente = ExtraerMinutosCierreVentana(secuencia[i + 1].VentanaHoraria);

            // Permitir reordenamiento dentro del mismo bloque horario (diferencia <= 120 min)
            if (cierreActual - cierreSiguiente > 120)
            {
                return false;
            }
        }

        return true;
    }

    private static int ExtraerMinutosCierreVentana(string? ventanaHoraria)
    {
        if (string.IsNullOrWhiteSpace(ventanaHoraria))
            return 13 * 60; // Por defecto 13:00 (780 min)

        var partes = ventanaHoraria.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string finTexto = partes.Length >= 2 ? partes[1] : partes[0];

        if (TimeSpan.TryParseExact(finTexto, @"hh\:mm", CultureInfo.InvariantCulture, out var ts))
        {
            return (int)ts.TotalMinutes;
        }

        return 13 * 60;
    }

    private static List<ParadaItemViewModel> EnsamblarSecuenciaConInmutables(
        IReadOnlyList<ParadaItemViewModel> paradasOriginales,
        List<ParadaItemViewModel> pendientesReordenadas)
    {
        var resultado = new List<ParadaItemViewModel>(paradasOriginales.Count);
        int indicePendiente = 0;

        foreach (var parada in paradasOriginales)
        {
            if (!EsParadaPendiente(parada.Estado))
            {
                // Parada completada (Entregado, Fallido, EnDevolucion): permanece inmutable en su posición histórica
                resultado.Add(parada);
            }
            else if (indicePendiente < pendientesReordenadas.Count)
            {
                resultado.Add(pendientesReordenadas[indicePendiente]);
                indicePendiente++;
            }
        }

        return resultado;
    }

    public static double CalcularDistanciaRecorridoKm(
        double latOrigen,
        double lonOrigen,
        IReadOnlyList<ParadaItemViewModel> paradas)
    {
        if (paradas.Count == 0)
            return 0.0;

        double totalKm = 0.0;
        double latActual = latOrigen;
        double lonActual = lonOrigen;

        foreach (var parada in paradas)
        {
            totalKm += CalcularDistanciaHaversineKm(latActual, lonActual, parada.Latitud, parada.Longitud);
            latActual = parada.Latitud;
            lonActual = parada.Longitud;
        }

        return totalKm;
    }

    private const double RadioTierraKm = 6371.0;

    public static double CalcularDistanciaHaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double rLat1 = lat1 * Math.PI / 180.0;
        double rLat2 = lat2 * Math.PI / 180.0;

        double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                   Math.Cos(rLat1) * Math.Cos(rLat2) *
                   Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return RadioTierraKm * c;
    }

    private static int CalcularTiempoEstimadoMinutos(double distanciaKm, int cantidadParadas)
    {
        double minutosConduccion = (distanciaKm / VelocidadUrbanaPromedioKmh) * 60.0;
        double minutosOperativos = cantidadParadas * MinutosOperativosPorParada;
        return (int)Math.Round(minutosConduccion + minutosOperativos, MidpointRounding.AwayFromZero);
    }

    private static bool EsParadaPendiente(string? estado)
    {
        return string.IsNullOrWhiteSpace(estado)
            || string.Equals(estado, "Pendiente", StringComparison.OrdinalIgnoreCase)
            || string.Equals(estado, "EnCamino", StringComparison.OrdinalIgnoreCase)
            || string.Equals(estado, "EnSitio", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SecuenciasIguales(
        IReadOnlyList<ParadaItemViewModel> a,
        IReadOnlyList<ParadaItemViewModel> b)
    {
        if (a.Count != b.Count)
            return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Id != b[i].Id && a[i].CodigoSeguimiento != b[i].CodigoSeguimiento)
                return false;
        }

        return true;
    }

    private static string FormatearResumenComparativo(
        double distanciaBaseKm,
        int tiempoBaseMin,
        double distanciaOptKm,
        int tiempoOptMin,
        double ahorroKm,
        double porcentajeAhorro,
        int ahorroMin,
        List<ParadaItemViewModel> pendientesBase,
        List<ParadaItemViewModel> pendientesOpt,
        int inmutablesCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Resumen de Optimización de Ruta");
        sb.AppendLine("-----------------------------------------------");
        sb.AppendLine($"Criterio Estándar de Despacho:  {distanciaBaseKm:F1} km (~{tiempoBaseMin} min)");
        sb.AppendLine($"Ruta Optimizada (Heurística 2-Opt):  {distanciaOptKm:F1} km (~{tiempoOptMin} min)");
        sb.AppendLine("-----------------------------------------------");
        sb.AppendLine($"Ahorro Medido en Recorrido:      -{porcentajeAhorro:F1}% (-{ahorroKm:F1} km)");
        sb.AppendLine($"Tiempo Estimado Reducido:        -{ahorroMin} minutos");
        sb.AppendLine();
        sb.AppendLine($"Orden Estándar:   {string.Join(" -> ", pendientesBase.Select(p => p.CodigoSeguimiento))}");
        sb.AppendLine($"Orden Optimizado: {string.Join(" -> ", pendientesOpt.Select(p => p.CodigoSeguimiento))}");

        if (inmutablesCount > 0)
        {
            sb.AppendLine($"Paradas completadas fijas: {inmutablesCount} (inmutables)");
        }

        return sb.ToString().TrimEnd();
    }
}

