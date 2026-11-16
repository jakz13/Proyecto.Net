namespace UltimaMilla.Integration.Tests;

/// <summary>Cédulas de prueba distintas en cada llamada: la API no admite dos repartidores con la misma cédula en un operador.</summary>
public static class CedulaDePrueba
{
    public static string Nueva()
    {
        // Ocho dígitos, solo números: el formato que exige el sistema
        return Random.Shared.Next(10_000_000, 99_999_999).ToString();
    }
}
