namespace UltimaMilla.Application.Common;

/// <summary>
/// Política mínima para las contraseñas que se crean o cambian desde la plataforma: largo, variedad de caracteres y que no sea una
/// contraseña trivial. Devuelve el motivo del rechazo (listo para mostrar) o <c>null</c> si la contraseña es aceptable.
/// </summary>
public static class PoliticaDePassword
{
    public const int LongitudMinima = 10;

    private static readonly HashSet<string> Triviales = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "contrasena", "contraseña", "1234567890", "qwertyuiop", "admin12345", "letmein123", "bienvenido1",
        "password123", "contrasena123", "administrador"
    };

    public static string? Validar(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "La contraseña no puede ser vacía.";

        var valor = password.Trim();
        if (valor.Length < LongitudMinima)
            return $"La contraseña debe tener al menos {LongitudMinima} caracteres.";

        var clases = 0;
        if (valor.Any(char.IsLower)) clases++;
        if (valor.Any(char.IsUpper)) clases++;
        if (valor.Any(char.IsDigit)) clases++;
        if (valor.Any(c => !char.IsLetterOrDigit(c))) clases++;
        if (clases < 3)
            return "La contraseña debe combinar al menos tres de estos tipos: minúsculas, mayúsculas, números y símbolos.";

        if (Triviales.Contains(valor) || valor.Distinct().Count() <= 3)
            return "La contraseña es demasiado fácil de adivinar.";

        return null;
    }
}
