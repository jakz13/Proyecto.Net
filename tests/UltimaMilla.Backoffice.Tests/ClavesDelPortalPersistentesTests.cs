using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// El Portal del comercio guardaba sus claves de protección de datos dentro del contenedor: cada despliegue las perdía y los formularios y las sesiones
/// abiertas fallaban ("The antiforgery token could not be decrypted") hasta limpiar las cookies. Como el Backoffice, debe guardarlas en un volumen.
/// </summary>
public class ClavesDelPortalPersistentesTests
{
    private static string Leer(string relativa)
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.prod.yml")))
            carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);
        return File.ReadAllText(Path.Combine(carpeta!.FullName, relativa));
    }

    [Fact]
    public void ElPortal_GuardaSusClavesEnLaRutaConfigurada()
    {
        var programa = Leer("src/Web/PortalComercio/Program.cs");

        Assert.Contains("PersistKeysToFileSystem", programa);
        Assert.Contains("DataProtection:Path", programa);
    }

    [Fact]
    public void ElDespliegue_MontaUnVolumenParaLasClavesDelPortal()
    {
        var compose = Leer("docker-compose.prod.yml");
        var portal = compose.Substring(compose.IndexOf("  portal-comercio:", StringComparison.Ordinal), 1500);

        Assert.Contains("DataProtection__Path: /keys", portal);
        Assert.Contains("portal_keys:/keys", portal);
        Assert.Contains("\n  portal_keys:", compose);   // declarado en la sección de volúmenes
    }
}
