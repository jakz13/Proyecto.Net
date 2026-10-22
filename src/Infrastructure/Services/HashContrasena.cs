using System.Security.Cryptography;

namespace UltimaMilla.Infrastructure.Services;

/// <summary>Hash de contraseñas con PBKDF2-SHA256 y sal aleatoria; el formato guardado es "iteraciones.sal.hash" en Base64.</summary>
public static class HashContrasena
{
    private const int Iteraciones = 100_000;
    private const int TamanoSal = 16;
    private const int TamanoHash = 32;

    public static string Generar(string contrasena)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanoSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, Iteraciones, HashAlgorithmName.SHA256, TamanoHash);
        return $"{Iteraciones}.{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string contrasena, string hashGuardado)
    {
        var partes = hashGuardado.Split('.');
        if (partes.Length != 3 || !int.TryParse(partes[0], out var iteraciones))
            return false;

        var sal = Convert.FromBase64String(partes[1]);
        var esperado = Convert.FromBase64String(partes[2]);
        var calculado = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }
}
