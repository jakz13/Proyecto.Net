namespace UltimaMilla.Application.Tablero;

public enum NivelRiesgo
{
    Moderado,
    Alto,
    Critico
}

public record RiesgoEvaluado(NivelRiesgo Nivel, int MinutosRetraso, string Causa);

/// <summary>Punto de una ruta; sin coordenadas el tramo se estima con un tiempo fijo.</summary>
public readonly record struct PuntoRuta(double? Latitud, double? Longitud);

/// <summary>
/// Criterio con el que el tablero (RF-29) decide qué envíos están en riesgo de incumplir su ventana comprometida.
/// Es una estimación, no una optimización: distancia en línea recta corregida por un factor urbano, a una velocidad
/// media fija, más un tiempo de servicio por cada parada anterior. Los parámetros son constantes visibles para poder
/// documentarlos y ajustarlos.
/// </summary>
public static class EvaluadorRiesgoVentana
{
    public const double VelocidadPromedioKmh = 25;
    public const double FactorSinuosidadUrbana = 1.3;
    public const int MinutosPorParada = 8;
    public const int MinutosTramoSinCoordenadas = 10;

    /// <summary>Con menos de este margen respecto del límite de la ventana el envío ya se marca como riesgo moderado.</summary>
    public const int MargenPreventivoMinutos = 15;
    public const int RetrasoAltoMinutos = 10;
    public const int RetrasoCriticoMinutos = 30;

    /// <summary>
    /// Estima la hora de llegada a la última parada del recorrido. <paramref name="recorrido"/> son las paradas
    /// pendientes en orden, terminando en la que se evalúa; <paramref name="origen"/> es la última posición conocida.
    /// </summary>
    public static DateTime EstimarLlegada(DateTime ahora, PuntoRuta? origen, IReadOnlyList<PuntoRuta> recorrido)
    {
        if (recorrido.Count == 0)
            return ahora;

        var minutos = 0.0;
        var anterior = origen;
        for (var i = 0; i < recorrido.Count; i++)
        {
            minutos += MinutosDeTramo(anterior, recorrido[i]);
            if (i < recorrido.Count - 1)
                minutos += MinutosPorParada;
            anterior = recorrido[i];
        }

        return ahora.AddMinutes(Math.Ceiling(minutos));
    }

    /// <summary>Devuelve el riesgo, o null si la llegada estimada deja margen suficiente antes del límite de la ventana.</summary>
    public static RiesgoEvaluado? Evaluar(DateTime ahora, DateTime horaLimiteVentana, DateTime llegadaEstimada, string? incidenciaAbierta = null)
    {
        var retraso = (int)Math.Ceiling((llegadaEstimada - horaLimiteVentana).TotalMinutes);
        var sufijo = string.IsNullOrWhiteSpace(incidenciaAbierta) ? string.Empty : $" Hay una incidencia abierta en la ruta ({incidenciaAbierta}).";

        if (ahora > horaLimiteVentana)
            return new RiesgoEvaluado(NivelRiesgo.Critico, Math.Max(retraso, 1), "La ventana comprometida ya venció y el envío sigue pendiente." + sufijo);

        if (retraso >= RetrasoCriticoMinutos)
            return new RiesgoEvaluado(NivelRiesgo.Critico, retraso, $"Llegada estimada {retraso} min después del límite de la ventana." + sufijo);

        if (retraso >= RetrasoAltoMinutos)
            return new RiesgoEvaluado(NivelRiesgo.Alto, retraso, $"Llegada estimada {retraso} min después del límite de la ventana." + sufijo);

        if (retraso > 0)
            return new RiesgoEvaluado(NivelRiesgo.Moderado, retraso, $"Llegada estimada {retraso} min después del límite de la ventana." + sufijo);

        if (retraso > -MargenPreventivoMinutos)
            return new RiesgoEvaluado(NivelRiesgo.Moderado, 0, $"Llegada estimada con menos de {MargenPreventivoMinutos} min de margen antes del límite de la ventana." + sufijo);

        return null;
    }

    public static double DistanciaKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double radioTierraKm = 6371.0;
        var dLat = Radianes(lat2 - lat1);
        var dLon = Radianes(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(Radianes(lat1)) * Math.Cos(Radianes(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return radioTierraKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double MinutosDeTramo(PuntoRuta? desde, PuntoRuta hasta)
    {
        if (desde is { Latitud: { } la, Longitud: { } lo } && hasta is { Latitud: { } lb, Longitud: { } lg })
            return DistanciaKm(la, lo, lb, lg) * FactorSinuosidadUrbana / VelocidadPromedioKmh * 60;

        return MinutosTramoSinCoordenadas;
    }

    private static double Radianes(double grados) => grados * Math.PI / 180;
}
