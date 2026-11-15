using System.Text.RegularExpressions;
using Xunit;

namespace UltimaMilla.Architecture.Tests;

/// <summary>
/// Actualización de la app del repartidor por descarga directa (OTA): Caddy sirve el APK firmado y version.json desde una carpeta de la VM. Son reglas de
/// configuración y de scripts que ninguna prueba de código ve: si fallan, el pipeline pasa pero la app no puede actualizarse (o se publica algo equivocado).
/// </summary>
public class DescargasDeLaAppTests
{
    private static string Raiz()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.prod.yml")))
            carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);
        return carpeta!.FullName;
    }

    // Los archivos pueden tener saltos de línea de Windows (CRLF) según cómo los haya dejado Git en cada PC
    private static string Leer(string relativa) => File.ReadAllText(Path.Combine(Raiz(), relativa)).Replace("\r\n", "\n");

    private static string BloqueDescargas(string caddyfile)
    {
        var inicio = caddyfile.IndexOf("(descargas) {", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "El Caddyfile no define el bloque (descargas).");
        var fin = caddyfile.IndexOf("\n}\n", inicio, StringComparison.Ordinal);
        return caddyfile[inicio..fin];
    }

    [Fact]
    public void Caddy_MontaLaCarpetaDeDescargasSoloLectura()
    {
        var compose = Leer("docker-compose.prod.yml");
        var caddy = compose[compose.IndexOf("\n  caddy:", StringComparison.Ordinal)..compose.IndexOf("\nvolumes:", StringComparison.Ordinal)];

        Assert.Contains("./descargas:/srv/descargas:ro", caddy);
    }

    [Fact]
    public void ElCaddyfile_ServeDescargasConVersionSinCacheYApkConSuTipoMime()
    {
        var bloque = BloqueDescargas(Leer("docker/Caddyfile.prod"));

        Assert.Contains("handle /descargas/*", bloque);
        Assert.Contains("root * /srv", bloque);
        Assert.Contains("file_server", bloque);
        Assert.DoesNotContain("browse", bloque);   // sin listado de la carpeta
        Assert.Matches(@"@version path /descargas/version\.json", bloque);
        Assert.Matches(@"header @version Cache-Control ""no-store, no-cache, must-revalidate""", bloque);
        Assert.Matches(@"header @apk Content-Type ""application/vnd\.android\.package-archive""", bloque);
    }

    [Fact]
    public void ElDominioPrincipal_ImportaLasDescargasAntesDelProxyDeLaApi()
    {
        var caddyfile = Leer("docker/Caddyfile.prod");
        var bloquePrincipal = caddyfile[caddyfile.IndexOf("{$DOMINIO} {", StringComparison.Ordinal)..caddyfile.IndexOf("{$DOMINIO_COMERCIO} {", StringComparison.Ordinal)];

        var importar = bloquePrincipal.IndexOf("import descargas", StringComparison.Ordinal);
        Assert.True(importar >= 0, "El dominio principal no importa las descargas.");
        Assert.True(importar < bloquePrincipal.IndexOf("@api", StringComparison.Ordinal), "Las descargas deben resolverse antes del proxy hacia la API.");
        Assert.DoesNotContain("import descargas", caddyfile[caddyfile.IndexOf("{$DOMINIO_COMERCIO} {", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void PublicarApk_SubeElApkAntesQueVersionJsonYVerificaLaCopiaYLaPublicacion()
    {
        var script = Leer("scripts/publicar-apk.ps1");

        // el sha256 de la VM debe coincidir antes de publicar, y version.json se mueve DESPUÉS que el APK: la app nunca ve una versión sin su APK
        Assert.Contains("sha256sum", script);
        var mover = script.IndexOf("mv -f .$nombreApk.tmp $nombreApk && mv -f .version.json.tmp version.json", StringComparison.Ordinal);
        Assert.True(mover >= 0, "El APK y version.json deben renombrarse en ese orden (version.json al final).");
        // no publica una versión que Android no instalaría como actualización
        Assert.Matches(@"versionCode -and -not \$Forzar|\$versionCode -and -not \$Forzar", script);
        Assert.Contains("no es mayor que el publicado", script);
        // verificación por HTTPS
        Assert.Contains("no-store", script);
        Assert.Contains("application/vnd.android.package-archive", script);
        Assert.Contains("Content-Length", script);
        Assert.Contains("sha256", script);
    }

    [Fact]
    public void PublicarApk_EsSoloAscii_PorqueWindowsPowerShell51LeeLosArchivosSinBomComoAnsi()
    {
        var bytes = File.ReadAllBytes(Path.Combine(Raiz(), "scripts/publicar-apk.ps1"));

        Assert.All(bytes, b => Assert.True(b < 128, "scripts/publicar-apk.ps1 tiene caracteres no ASCII (tildes o ñ): PowerShell 5.1 los corrompe."));
    }

    [Fact]
    public void ElDespliegueNormal_NuncaPublicaUnApkPorSuCuenta()
    {
        // artifacts/ es local de cada PC: un despliegue de la API que publicara el APK que haya quedado ahí subiría una versión vieja o sin terminar
        var desplegar = Leer("scripts/desplegar.ps1");

        Assert.DoesNotContain("Release-Signed", desplegar);
        Assert.Matches(@"if \(\$Servicios -contains 'mobile'\)[\s\S]*?publicar-apk\.ps1[\s\S]*?return", desplegar);
        Assert.Contains("'mobile'", Regex.Match(desplegar, @"ValidateSet\([^)]*\)").Value);
    }

    [Theory]
    [InlineData("scripts/desplegar.ps1")]
    [InlineData("scripts/desplegar.sh")]
    public void ElDespliegue_CreaLaCarpetaDeDescargasAntesDeQueDockerLaCreeComoRoot(string script)
    {
        Assert.Contains("~/shippingbox/descargas", Leer(script));
    }
}
