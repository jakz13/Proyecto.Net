using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Las cuentas y claves de demostración figuran en el repositorio: en producción no pueden crearse con esos valores.
/// Las pruebas cambian variables de entorno del proceso, por eso comparten colección (no corren en paralelo con otras).
/// </summary>
[Collection(ColeccionEntorno.Nombre)]
public class PoliticaDeSemillasTests : IDisposable
{
    private readonly string? _entorno = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    private readonly string? _password = Environment.GetEnvironmentVariable(PoliticaDeSemillas.VariablePassword);
    private readonly string? _claveApi = Environment.GetEnvironmentVariable(PoliticaDeSemillas.VariableClaveApi);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _entorno);
        Environment.SetEnvironmentVariable(PoliticaDeSemillas.VariablePassword, _password);
        Environment.SetEnvironmentVariable(PoliticaDeSemillas.VariableClaveApi, _claveApi);
    }

    private static void Configurar(string? entorno, string? password = null, string? claveApi = null)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", entorno);
        Environment.SetEnvironmentVariable(PoliticaDeSemillas.VariablePassword, password);
        Environment.SetEnvironmentVariable(PoliticaDeSemillas.VariableClaveApi, claveApi);
    }

    [Fact]
    public void EnProduccion_SinContrasenaDelDespliegue_NoSeCreanCuentasDeDemostracion()
    {
        Configurar("Production");

        Assert.False(PoliticaDeSemillas.DebeSembrarUsuarios);
    }

    [Fact]
    public void EnProduccion_UnaContrasenaCortaNoSirve_YUnaLargaSi()
    {
        Configurar("Production", password: "corta123");
        Assert.Null(PoliticaDeSemillas.PasswordConfigurada());
        Assert.False(PoliticaDeSemillas.DebeSembrarUsuarios);

        Configurar("Production", password: "UnaClaveLarga.2026!");
        Assert.Equal("UnaClaveLarga.2026!", PoliticaDeSemillas.PasswordConfigurada());
        Assert.True(PoliticaDeSemillas.DebeSembrarUsuarios);
        Assert.Equal("UnaClaveLarga.2026!", PoliticaDeSemillas.PasswordDe("Admin.2026!"));
    }

    [Fact]
    public void EnProduccion_LaClaveDeApiDeDemostracionNuncaEsLaPublicada()
    {
        Configurar("Production");

        var primera = PoliticaDeSemillas.ClaveApiDe("dev-api-key-megastore-2026");
        var segunda = PoliticaDeSemillas.ClaveApiDe("dev-api-key-megastore-2026");

        Assert.NotEqual("dev-api-key-megastore-2026", primera);
        Assert.NotEqual(primera, segunda); // aleatoria: nadie puede adivinarla ni repetirla
        Assert.True(primera.Length >= 32);

        Configurar("Production", claveApi: "clave-propia-del-despliegue-0123456789");
        Assert.Equal("clave-propia-del-despliegue-0123456789", PoliticaDeSemillas.ClaveApiDe("dev-api-key-megastore-2026"));
    }

    [Fact]
    public void FueraDeProduccion_SeConservanLosValoresDeLaDocumentacion()
    {
        Configurar("Development");

        Assert.True(PoliticaDeSemillas.DebeSembrarUsuarios);
        Assert.Equal("Admin.2026!", PoliticaDeSemillas.PasswordDe("Admin.2026!"));
        Assert.Equal("dev-api-key-megastore-2026", PoliticaDeSemillas.ClaveApiDe("dev-api-key-megastore-2026"));
    }
}

/// <summary>Colección sin paralelismo para las pruebas que modifican variables de entorno del proceso.</summary>
[CollectionDefinition(Nombre, DisableParallelization = true)]
public class ColeccionEntorno
{
    public const string Nombre = "EntornoDelProceso";
}
