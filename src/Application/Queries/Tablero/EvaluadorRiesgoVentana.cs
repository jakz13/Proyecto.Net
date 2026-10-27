namespace UltimaMilla.Application.Queries.Tablero;

/// <summary>Resultado de evaluar el riesgo de una parada pendiente frente a su ventana comprometida.</summary>
public record RiesgoVentana(string Nivel, int MinutosParaCierreVentana, string Causa);

/// <summary>
/// Regla de riesgo del tablero (UC-12). La plataforma no calcula tiempos de viaje, así que el riesgo se estima solo con datos reales:
/// la hora actual, el cierre de la ventana comprometida y la cantidad de paradas que quedan por atender hasta llegar a ese envío.
/// <list type="bullet">
/// <item><description>Crítico: la ventana ya venció y el envío sigue pendiente.</description></item>
/// <item><description>Alto: el margen es menor a la mitad del tiempo estimado de atención de las paradas previas.</description></item>
/// <item><description>Moderado: el margen no alcanza el tiempo estimado de atención de las paradas previas.</description></item>
/// </list>
/// </summary>
public static class EvaluadorRiesgoVentana
{
    /// <summary>Tiempo de servicio supuesto por parada (traslado y entrega). Es una estimación gruesa, no un dato medido.</summary>
    public const int MinutosEstimadosPorParada = 15;

    /// <param name="ahoraLocal">Hora local actual de la operación.</param>
    /// <param name="cierreVentanaLocal">Hora local en que cierra la ventana comprometida del envío.</param>
    /// <param name="paradasPendientesHastaEsta">Paradas por atender hasta llegar a este envío, incluida la propia (mínimo 1).</param>
    /// <returns>El riesgo detectado o <c>null</c> si el envío llega a tiempo.</returns>
    public static RiesgoVentana? Evaluar(DateTime ahoraLocal, DateTime cierreVentanaLocal, int paradasPendientesHastaEsta)
    {
        var paradas = Math.Max(1, paradasPendientesHastaEsta);
        var margen = (int)Math.Floor((cierreVentanaLocal - ahoraLocal).TotalMinutes);

        if (margen < 0)
            return new RiesgoVentana("Critico", margen, $"La ventana comprometida cerró hace {-margen} min y el envío sigue pendiente.");

        var necesario = paradas * MinutosEstimadosPorParada;
        if (margen >= necesario)
            return null;

        var nivel = margen * 2 < necesario ? "Alto" : "Moderado";
        return new RiesgoVentana(
            nivel,
            margen,
            $"Quedan {margen} min de ventana y {paradas} parada(s) por atender hasta este envío (≈{necesario} min estimados).");
    }
}
