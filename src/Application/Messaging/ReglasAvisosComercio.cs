using System.Security.Cryptography;
using System.Text;

namespace UltimaMilla.Application.Messaging;

/// <summary>Firma HMAC-SHA256 de los avisos a comercios (§6.9): permite al receptor verificar el origen del mensaje.</summary>
public static class FirmaWebhook
{
    public const string Prefijo = "sha256=";
    public const string CabeceraFirma = "X-Hub-Signature-256";
    public const string CabeceraIdAviso = "X-Aviso-Id";

    public static string Calcular(string claveFirma, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claveFirma);
        ArgumentNullException.ThrowIfNull(payload);

        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(claveFirma), Encoding.UTF8.GetBytes(payload));
        return Prefijo + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Verificación en tiempo constante, para el receptor de prueba y para los tests.</summary>
    public static bool EsValida(string claveFirma, string payload, string? firmaRecibida)
    {
        if (string.IsNullOrWhiteSpace(claveFirma) || payload is null || string.IsNullOrWhiteSpace(firmaRecibida))
            return false;

        var esperada = Encoding.UTF8.GetBytes(Calcular(claveFirma, payload));
        return CryptographicOperations.FixedTimeEquals(esperada, Encoding.UTF8.GetBytes(firmaRecibida.Trim().ToLowerInvariant()));
    }
}

/// <summary>Política de reintentos con espera creciente para los avisos a comercios: 1 min, 5 min y 15 min, hasta 4 intentos en total.</summary>
public static class PoliticaReintentoAvisos
{
    public const int MaximoIntentos = 4;

    private static readonly TimeSpan[] Esperas = { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15) };

    /// <summary>Indica si, tras fallar el intento número <c>intentosPrevios + 1</c>, ya no quedan reintentos automáticos.</summary>
    public static bool SeAgotan(int intentosPrevios) => intentosPrevios + 1 >= MaximoIntentos;

    /// <summary>Espera hasta el próximo intento, dado el número de intentos ya realizados antes del que acaba de fallar.</summary>
    public static TimeSpan EsperaDespuesDeFallar(int intentosPrevios)
    {
        var indice = Math.Clamp(intentosPrevios, 0, Esperas.Length - 1);
        return Esperas[indice];
    }
}
