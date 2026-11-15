using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace UltimaMilla.Mobile.Services.Updates;

/// <summary>
/// Reglas de seguridad de la actualización por descarga directa, sin dependencias de MAUI para poder probarlas fuera del teléfono
/// (<c>tests/UltimaMilla.Backoffice.Tests</c> compila este archivo). Principio: ante cualquier duda se rechaza, nunca se instala.
/// </summary>
public static class PoliticaDeActualizacion
{
    /// <summary>Únicos servidores de los que la app acepta un APK. El manifiesto no puede mandarla a otro lado.</summary>
    public static readonly string[] HostsPermitidos = { "shippingbox.duckdns.org" };

    /// <summary>La URL del APK debe ser https, de un host permitido y sin credenciales incrustadas.</summary>
    public static bool EsUrlApkPermitida(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttps
            && string.IsNullOrEmpty(uri.UserInfo)
            && HostsPermitidos.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Un SHA-256 son 64 dígitos hexadecimales (se admiten guiones y espacios, que se ignoran).</summary>
    public static bool EsSha256Valido(string? sha256)
    {
        var limpio = NormalizarSha256(sha256);
        return limpio.Length == 64 && limpio.All(Uri.IsHexDigit);
    }

    /// <summary>Compara la huella esperada (del manifiesto) con la calculada. Si la esperada falta o no es válida, NO coincide: no se instala sin verificar.</summary>
    public static bool Sha256Coincide(string? esperado, string calculado)
        => EsSha256Valido(esperado) && string.Equals(NormalizarSha256(esperado), NormalizarSha256(calculado), StringComparison.OrdinalIgnoreCase);

    /// <summary>Si el manifiesto declara un tamaño, el archivo descargado debe medir exactamente eso.</summary>
    public static bool TamanoCoincide(long? esperado, long real) => esperado is null or <= 0 || esperado == real;

    /// <summary>Huella SHA-256 (hexadecimal) de un archivo, para compararla con la del manifiesto.</summary>
    public static async Task<string> CalcularSha256Async(Stream contenido, CancellationToken cancelacion = default)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(await sha256.ComputeHashAsync(contenido, cancelacion));
    }

    private static string NormalizarSha256(string? valor)
        => (valor ?? string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).Trim();
}
