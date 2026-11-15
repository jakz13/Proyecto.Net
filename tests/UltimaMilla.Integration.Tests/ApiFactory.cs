using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UltimaMilla.Application.Ports;

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
        // Las pruebas inician sesión muchas veces desde la misma IP: el tope de producción las bloquearía
        Environment.SetEnvironmentVariable("RateLimit__LoginPorMinuto", "100000");
        // Sin salida a Internet no hay geocodificador: la regla de cobertura se prueba aparte, con un geocodificador falso (VerificadorCoberturaTests)
        Environment.SetEnvironmentVariable("Cobertura__Validar", "false");
    }

    /// <summary>
    /// Las cuentas de prueba (admin, comercio, depósito, despachador...) las comparten todos los tests de una clase y el helper <see cref="UsuariosDePrueba"/>
    /// vuelve a iniciar sesión como admin para crear las cuentas que faltan: con la política de sesión única activa eso expulsaba el token de admin que el propio test
    /// ya tenía (401 SESION_DESPLAZADA, solo en una base limpia como la del pipeline). Por eso la fábrica ignora el registro de la sesión activa; los tests de la
    /// propia política la activan con <c>Pruebas:SesionUnicaReal=true</c> y cuentas propias (<see cref="SesionUnicaActivaApiTests"/>).
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(servicios =>
        {
            var original = servicios.LastOrDefault(d => d.ServiceType == typeof(IDistributedCacheService));
            if (original is null)
                return;

            servicios.Remove(original);
            servicios.Add(ServiceDescriptor.Describe(typeof(IDistributedCacheService), proveedor =>
            {
                var real = (IDistributedCacheService)(original.ImplementationFactory?.Invoke(proveedor)
                    ?? ActivatorUtilities.CreateInstance(proveedor, original.ImplementationType!));
                var sesionUnicaReal = proveedor.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["Pruebas:SesionUnicaReal"] == "true";
                return sesionUnicaReal ? real : new CacheSinSesionUnica(real);
            }, original.Lifetime));
        });
    }

    /// <summary>Delega todo en el caché real salvo la clave de sesión activa por usuario, que no guarda ni devuelve nunca.</summary>
    private sealed class CacheSinSesionUnica : IDistributedCacheService
    {
        private const string PrefijoSesionActiva = "sesion:activa:usuario:";
        private readonly IDistributedCacheService _real;

        public CacheSinSesionUnica(IDistributedCacheService real) => _real = real;

        private static bool EsSesionActiva(string clave) => clave.StartsWith(PrefijoSesionActiva, StringComparison.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => EsSesionActiva(key) ? Task.FromResult<T?>(default) : _real.GetAsync<T>(key, cancellationToken);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
            => EsSesionActiva(key) ? Task.CompletedTask : _real.SetAsync(key, value, expiration, cancellationToken);

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
            => EsSesionActiva(key) ? Task.CompletedTask : _real.RemoveAsync(key, cancellationToken);
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
