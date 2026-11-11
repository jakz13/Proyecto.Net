using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Infrastructure.Services;

namespace UltimaMilla.Infrastructure.Persistence;

/// <summary>
/// Reglas de los datos de demostración sembrados al arrancar. Las cuentas y claves de demostración figuran en el repositorio, así que
/// <b>no pueden existir con esos valores en un ambiente real</b>:
/// <list type="bullet">
/// <item>En producción solo se crean usuarios si se define <c>SEED_PASSWORD_DEMO</c> (mínimo 12 caracteres); sin ella no se crea ninguno.</item>
/// <item>En producción las claves de API de los comercios de demostración son aleatorias (o la de <c>SEED_API_KEY_DEMO</c>), nunca las publicadas.</item>
/// <item>Si el ambiente ya tenía cuentas o claves con los valores publicados, al arrancar se rotan.</item>
/// </list>
/// En desarrollo y pruebas se conservan los valores conocidos de la documentación.
/// </summary>
public static class PoliticaDeSemillas
{
    public const string VariablePassword = "SEED_PASSWORD_DEMO";
    public const string VariableClaveApi = "SEED_API_KEY_DEMO";

    private const int LongitudMinimaProduccion = 12;
    private const int LongitudMinimaDesarrollo = 8;
    private const int LongitudMinimaClaveApi = 24;

    // Cuentas de demostración con la contraseña que figura en el repositorio
    private static readonly (string Email, string PasswordPublicada)[] CuentasDemo =
    {
        ("admin@gmail.com", "Admin.2026!"),
        ("despacho@gmail.com", "Despacho.2026!"),
        ("deposito@gmail.com", "Deposito.2026!"),
        ("repartidor@gmail.com", "Repartidor.2026!"),
        ("comercio@gmail.com", "Comercio.2026!"),
        ("admin2@gmail.com", "Admin.2026!"),
        (DatabaseSeeder.EmailAdministradorPlataforma, "Plataforma.2026!"),
        (DatabaseSeederSegundoOperador.EmailAdministrador, DatabaseSeederAdministracion.PasswordDemo),
        (DatabaseSeederSegundoOperador.EmailDespachador, DatabaseSeederAdministracion.PasswordDemo),
    };

    private static readonly string[] ClavesApiPublicadas =
    {
        DatabaseSeeder.ClaveApiDemo,
        DatabaseSeederSegundoOperador.ClaveApiDemo,
    };

    public static bool EsProduccion =>
        string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Production", StringComparison.OrdinalIgnoreCase);

    /// <summary>La contraseña definida por el despliegue para las cuentas de demostración, o <c>null</c> si no hay una válida.</summary>
    public static string? PasswordConfigurada()
    {
        var definida = Environment.GetEnvironmentVariable(VariablePassword);
        var minimo = EsProduccion ? LongitudMinimaProduccion : LongitudMinimaDesarrollo;
        return definida is { } p && p.Length >= minimo ? p : null;
    }

    /// <summary>Fuera de producción siempre; en producción, solo con una contraseña propia del despliegue.</summary>
    public static bool DebeSembrarUsuarios => !EsProduccion || PasswordConfigurada() is not null;

    public static string PasswordDe(string porDefecto) => PasswordConfigurada() ?? porDefecto;

    /// <summary>Clave de API para un comercio de demostración: la publicada fuera de producción; en producción, una propia o aleatoria.</summary>
    public static string ClaveApiDe(string publicada)
    {
        if (!EsProduccion)
            return publicada;

        var definida = Environment.GetEnvironmentVariable(VariableClaveApi);
        return definida is { } c && c.Length >= LongitudMinimaClaveApi
            ? c
            : Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    }

    /// <summary>
    /// Reemplaza las contraseñas y claves de API que todavía tienen un valor publicado en el repositorio. Las cuentas cuya contraseña
    /// ya fue cambiada por su dueño no se tocan.
    /// </summary>
    public static async Task RotarCredencialesPublicadasAsync(AppDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        var nueva = PasswordConfigurada();
        if (nueva is not null)
        {
            var hasher = new PasswordHasherService();
            var correos = CuentasDemo.Select(c => c.Email).ToList();
            var usuarios = await context.Usuarios.Where(u => correos.Contains(u.Email)).ToListAsync(cancellationToken);

            foreach (var usuario in usuarios)
            {
                var publicada = CuentasDemo.First(c => c.Email == usuario.Email).PasswordPublicada;
                if (hasher.VerificarPassword(publicada, usuario.PasswordHash))
                {
                    usuario.CambiarPassword(hasher.HashearPassword(nueva));
                    logger.LogWarning("Semillas: la cuenta de demostración {Email} tenía la contraseña publicada; se reemplazó por la del despliegue.", usuario.Email);
                }
            }
        }

        if (EsProduccion)
        {
            foreach (var publicada in ClavesApiPublicadas)
            {
                var huellaPublicada = ComercioOperador.CalcularHuellaClaveApi(publicada);
                var relaciones = await context.ComerciosOperadores.IgnoreQueryFilters()
                    .Where(c => c.ClaveApi == huellaPublicada)
                    .ToListAsync(cancellationToken);

                foreach (var relacion in relaciones)
                {
                    relacion.RegenerarClaveApi(ComercioOperador.CalcularHuellaClaveApi(ClaveApiDe(publicada)));
                    logger.LogWarning("Semillas: la relación comercio-operador {Id} tenía la clave de API publicada; se reemplazó.", relacion.Id);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
