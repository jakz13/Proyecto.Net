namespace UltimaMilla.Mobile.Responsive;

/// <summary>
/// Reglas puras (sin dependencias de MAUI) que deciden cómo adaptar la interfaz al ancho disponible.
/// Se mantienen aisladas para poder probarlas con xUnit sin levantar el framework de UI.
/// </summary>
public static class ReglasResponsive
{
    /// <summary>
    /// Ancho (en unidades independientes del dispositivo) por debajo del cual la pantalla se considera
    /// "compacta". Cubre los Android chicos de referencia (360 x 640 dp) y deja afuera los teléfonos
    /// estándar actuales (393 - 412 dp), que conservan el diseño original.
    /// </summary>
    public const double AnchoCompactoMaximo = 380d;

    /// <summary>Indica si el ancho de ventana corresponde a un dispositivo compacto.</summary>
    public static bool EsCompacto(double anchoVentana)
    {
        if (double.IsNaN(anchoVentana) || double.IsInfinity(anchoVentana) || anchoVentana <= 0)
            return false;

        return anchoVentana < AnchoCompactoMaximo;
    }

    /// <summary>
    /// Determina si un conjunto de elementos entra en una sola fila respetando su ancho natural.
    /// </summary>
    /// <param name="anchoDisponible">Ancho interior disponible para la fila.</param>
    /// <param name="anchosDeseados">Ancho natural (sin restricciones) de cada elemento.</param>
    /// <param name="espaciado">Separación horizontal entre elementos.</param>
    public static bool CabeEnFila(double anchoDisponible, IReadOnlyList<double> anchosDeseados, double espaciado)
    {
        ArgumentNullException.ThrowIfNull(anchosDeseados);

        if (anchosDeseados.Count <= 1)
            return true;

        if (double.IsPositiveInfinity(anchoDisponible))
            return true;

        if (double.IsNaN(anchoDisponible) || anchoDisponible <= 0)
            return false;

        var requerido = SumarAnchos(anchosDeseados) + EspaciadoTotal(anchosDeseados.Count, espaciado);
        return requerido <= anchoDisponible;
    }

    /// <summary>
    /// Reparte el ancho disponible entre los elementos de una fila: cada uno recibe su ancho natural
    /// más una porción igual del espacio sobrante, de modo que la fila ocupe todo el ancho.
    /// Si no hay espacio sobrante, los anchos se reducen proporcionalmente (nunca negativos).
    /// </summary>
    public static double[] DistribuirAnchos(double anchoDisponible, IReadOnlyList<double> anchosDeseados, double espaciado)
    {
        ArgumentNullException.ThrowIfNull(anchosDeseados);

        var cantidad = anchosDeseados.Count;
        if (cantidad == 0)
            return [];

        var util = double.IsNaN(anchoDisponible) || anchoDisponible <= 0
            ? 0d
            : anchoDisponible - EspaciadoTotal(cantidad, espaciado);
        util = Math.Max(0d, util);

        var deseados = anchosDeseados.Select(Normalizar).ToArray();
        var totalDeseado = deseados.Sum();
        var sobrante = util - totalDeseado;

        if (sobrante >= 0)
            return deseados.Select(d => d + sobrante / cantidad).ToArray();

        if (totalDeseado <= 0)
            return Enumerable.Repeat(util / cantidad, cantidad).ToArray();

        var factor = util / totalDeseado;
        return deseados.Select(d => d * factor).ToArray();
    }

    private static double SumarAnchos(IReadOnlyList<double> anchos) => anchos.Sum(Normalizar);

    private static double EspaciadoTotal(int cantidad, double espaciado)
        => cantidad <= 1 ? 0d : Math.Max(0d, Normalizar(espaciado)) * (cantidad - 1);

    private static double Normalizar(double valor)
        => double.IsNaN(valor) || double.IsInfinity(valor) || valor < 0 ? 0d : valor;
}
