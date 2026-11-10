using System.Globalization;
using System.Text.RegularExpressions;

namespace UltimaMilla.SeguimientoPublico.Servicios;

/// <summary>
/// Gestiona el estado y la identidad visual dinámica del operador en la interfaz de seguimiento público.
/// Inicia con una paleta neutra e institucional, y adopta fluidamente los colores y marca del operador
/// únicamente cuando se consulta un paquete asignado.
/// </summary>
public class ContextoSeguimientoOperador
{
    // Colores institucionales neutros por defecto
    public const string ColorPrimarioDefecto = "#1E293B"; // Slate oscuro elegante
    public const string ColorSecundarioDefecto = "#2563EB"; // Azul tecnológico

    public string? NombreOperador { get; private set; }
    public string? LogoUrl { get; private set; }
    public string ColorPrimario { get; private set; } = ColorPrimarioDefecto;
    public string ColorSecundario { get; private set; } = ColorSecundarioDefecto;
    public string ColorTextoPrimario { get; private set; } = "#FFFFFF";
    public string ColorTextoSecundario { get; private set; } = "#FFFFFF";
    public bool TieneOperadorAsignado => !string.IsNullOrEmpty(NombreOperador);

    public event Action? OnCambioIdentidad;

    public void FijarOperador(string? nombre, string? colorPrimario, string? colorSecundario, string? logoUrl)
    {
        NombreOperador = string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim();
        LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();

        var primario = NormalizarHexadecimal(colorPrimario, ColorPrimarioDefecto);
        var secundario = NormalizarHexadecimal(colorSecundario, ColorSecundarioDefecto);

        ColorPrimario = primario;
        ColorSecundario = secundario;
        ColorTextoPrimario = ObtenerColorTextoParaFondo(primario);
        ColorTextoSecundario = ObtenerColorTextoParaFondo(secundario);

        OnCambioIdentidad?.Invoke();
    }

    public void RestablecerNeutral()
    {
        NombreOperador = null;
        LogoUrl = null;
        ColorPrimario = ColorPrimarioDefecto;
        ColorSecundario = ColorSecundarioDefecto;
        ColorTextoPrimario = "#FFFFFF";
        ColorTextoSecundario = "#FFFFFF";

        OnCambioIdentidad?.Invoke();
    }

    public static string NormalizarHexadecimal(string? hex, string defecto)
    {
        if (string.IsNullOrWhiteSpace(hex)) return defecto;
        var valor = hex.Trim();
        if (!valor.StartsWith('#')) valor = "#" + valor;
        return Regex.IsMatch(valor, @"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$") ? valor.ToUpperInvariant() : defecto;
    }

    public static string ObtenerColorTextoParaFondo(string? hexFondo)
    {
        if (string.IsNullOrWhiteSpace(hexFondo)) return "#FFFFFF";
        var limpio = hexFondo.TrimStart('#');
        if (limpio.Length == 3)
            limpio = string.Concat(limpio.Select(c => $"{c}{c}"));
        if (limpio.Length != 6) return "#FFFFFF";

        if (int.TryParse(limpio[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
            int.TryParse(limpio[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
            int.TryParse(limpio[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            var luminancia = (r * 299 + g * 587 + b * 114) / 1000;
            return luminancia > 140 ? "#0F172A" : "#FFFFFF";
        }

        return "#FFFFFF";
    }
}
