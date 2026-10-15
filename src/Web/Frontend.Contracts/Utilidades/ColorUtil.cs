using System.Globalization;
using System.Text.RegularExpressions;

namespace UltimaMilla.Frontend.Contracts.Utilidades;

/// <summary>
/// Utilidades para validación de colores hexadecimales y cálculo de contraste/luminancia
/// según las pautas de accesibilidad WCAG 2.1 para soporte de multitenancy visual.
/// </summary>
public static partial class ColorUtil
{
    // Expresión regular compilada para validar formato hexadecimal (#RGB o #RRGGBB)
    [GeneratedRegex("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex RegexHexadecimal();

    /// <summary>
    /// Valida si una cadena de texto corresponde a un color hexadecimal válido.
    /// </summary>
    public static bool EsHexadecimalValido(string? valorHexadecimal)
    {
        if (string.IsNullOrWhiteSpace(valorHexadecimal))
        {
            return false;
        }

        return RegexHexadecimal().IsMatch(valorHexadecimal.Trim());
    }

    /// <summary>
    /// Normaliza un código hexadecimal al formato estándar de 6 caracteres con almohadilla (#RRGGBB).
    /// Si el valor no es válido, retorna el color por defecto suministrado.
    /// </summary>
    public static string NormalizarHexadecimal(string? valorHexadecimal, string colorPorDefecto = "#1D4ED8")
    {
        if (!EsHexadecimalValido(valorHexadecimal))
        {
            return colorPorDefecto;
        }

        var valorLimpio = valorHexadecimal!.Trim();
        if (valorLimpio.Length == 4) // Formato #RGB
        {
            var r = valorLimpio[1];
            var g = valorLimpio[2];
            var b = valorLimpio[3];
            return $"#{r}{r}{g}{g}{b}{b}".ToUpperInvariant();
        }

        return valorLimpio.ToUpperInvariant();
    }

    /// <summary>
    /// Calcula la luminancia relativa de un color sRGB conforme a la fórmula estándar de WCAG 2.1.
    /// El resultado es un valor entre 0.0 (negro más oscuro) y 1.0 (blanco más claro).
    /// </summary>
    public static double CalcularLuminanciaRelativa(string valorHexadecimal)
    {
        var hexNormalizado = NormalizarHexadecimal(valorHexadecimal);

        var rByte = byte.Parse(hexNormalizado.Substring(1, 2), NumberStyles.HexNumber);
        var gByte = byte.Parse(hexNormalizado.Substring(3, 2), NumberStyles.HexNumber);
        var bByte = byte.Parse(hexNormalizado.Substring(5, 2), NumberStyles.HexNumber);

        var rLineal = ConvertirCanalALineal(rByte / 255.0);
        var gLineal = ConvertirCanalALineal(gByte / 255.0);
        var bLineal = ConvertirCanalALineal(bByte / 255.0);

        return (0.2126 * rLineal) + (0.7152 * gLineal) + (0.0722 * bLineal);
    }

    /// <summary>
    /// Determina si sobre el fondo dado debe colocarse texto claro u oscuro para asegurar el contraste WCAG.
    /// Retorna '#0F172A' (oscuro) si el fondo es claro, o '#FFFFFF' (blanco) si el fondo es oscuro.
    /// </summary>
    public static string ObtenerColorTextoParaFondo(string colorFondoHexadecimal)
    {
        var luminancia = CalcularLuminanciaRelativa(colorFondoHexadecimal);

        // Umbral estándar WCAG: colores con luminancia mayor a ~0.179 requieren texto oscuro
        return luminancia > 0.179 ? "#0F172A" : "#FFFFFF";
    }

    private static double ConvertirCanalALineal(double valorCanal)
    {
        return valorCanal <= 0.04045
            ? valorCanal / 12.92
            : Math.Pow((valorCanal + 0.055) / 1.055, 2.4);
    }
}
