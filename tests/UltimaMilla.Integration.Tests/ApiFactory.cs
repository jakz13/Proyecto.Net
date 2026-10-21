using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Levanta la API completa contra un Postgres real, con una base propia y descartable por clase de test.
/// Se conecta a ULTIMAMILLA_TEST_PG (por defecto, el Postgres local del docker-compose).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private const string ConexionPorDefecto = "Host=localhost;Port=5432;Username=postgres;Password=postgres";

    private readonly string _baseDeDatos = "ultimamilla_it_" + Guid.NewGuid().ToString("N")[..12];

    public static string ConexionAdministrativa =>
        Environment.GetEnvironmentVariable("ULTIMAMILLA_TEST_PG") ?? ConexionPorDefecto;

    private string ConexionDeLaBase => $"{ConexionAdministrativa};Database={_baseDeDatos}";

    /// <summary>Indica si hay un Postgres alcanzable; si no, los tests se omiten en vez de fallar.</summary>
    public static bool PostgresDisponible()
    {
        try
        {
            using var conexion = new NpgsqlConnection(ConexionAdministrativa + ";Database=postgres;Timeout=3");
            conexion.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public ApiFactory()
    {
        // La API lee la cadena de conexión al construir el host; se fija antes de crearlo
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", ConexionDeLaBase);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        try
        {
            NpgsqlConnection.ClearAllPools();
            using var conexion = new NpgsqlConnection(ConexionAdministrativa + ";Database=postgres");
            conexion.Open();
            using var comando = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_baseDeDatos}\" WITH (FORCE)", conexion);
            comando.ExecuteNonQuery();
        }
        catch
        {
            // Limpieza de mejor esfuerzo: una base de prueba huérfana no afecta a los resultados
        }
    }
}

/// <summary>Test que se omite cuando no hay un Postgres disponible.</summary>
public sealed class FactPostgresAttribute : FactAttribute
{
    public FactPostgresAttribute()
    {
        if (!ApiFactory.PostgresDisponible())
            Skip = "No hay un Postgres disponible (ULTIMAMILLA_TEST_PG).";
    }
}

/// <summary>Todos los tests de integración comparten una sola API y una sola base, y corren en serie.</summary>
[CollectionDefinition(Nombre)]
public sealed class ColeccionApi : ICollectionFixture<ApiFactory>
{
    public const string Nombre = "api";
}
