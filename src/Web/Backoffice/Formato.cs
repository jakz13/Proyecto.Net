using System.Globalization;

namespace UltimaMilla.Backoffice;

/// <summary>
/// Formato único de números para las pantallas, a la uruguaya: coma decimal y punto de miles (8.500 kg, 5,5 m³).
/// El servidor corre con cultura invariante, que escribe al revés (8,500 y 5.5): quien lee "8,5" entendía otra cosa. Solo es para mostrar:
/// lo que viaja en formularios, atributos de datos y JSON sigue siendo invariante.
/// </summary>
public static class Formato
{
    public static readonly CultureInfo UY = CultureInfo.GetCultureInfo("es-UY");

    public static string N(decimal valor, int decimales) => valor.ToString("N" + decimales, UY);

    public static string N(double valor, int decimales) => valor.ToString("N" + decimales, UY);

    /// <summary>Número para atributos de datos que lee JavaScript (punto decimal, sin separador de miles).</summary>
    public static string Dato(decimal valor) => valor.ToString(CultureInfo.InvariantCulture);

    public static string Dato(double valor) => valor.ToString(CultureInfo.InvariantCulture);
}
