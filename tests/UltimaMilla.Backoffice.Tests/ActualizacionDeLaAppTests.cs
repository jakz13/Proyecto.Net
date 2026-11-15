using System.Text;
using System.Text.RegularExpressions;
using UltimaMilla.Mobile.Services.Updates;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// La app del repartidor se actualiza descargando un APK de https://shippingbox.duckdns.org/descargas. Reglas de seguridad (ante la duda se rechaza) y contrato
/// del <c>version.json</c> entre el script que lo publica y la app. La app MAUI no compila fuera de un entorno Android: estas pruebas cubren la lógica pura.
/// </summary>
public class ActualizacionDeLaAppTests
{
    private static string Raiz()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.prod.yml")))
            carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);
        return carpeta!.FullName;
    }

    private const string ShaValido = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Theory]
    [InlineData("https://shippingbox.duckdns.org/descargas/UltimaMilla-Release-Signed.apk", true)]
    [InlineData("HTTPS://SHIPPINGBOX.DUCKDNS.ORG/descargas/app.apk", true)]
    [InlineData("http://shippingbox.duckdns.org/descargas/app.apk", false)]               // sin TLS
    [InlineData("https://evil.example.com/descargas/app.apk", false)]                     // otro servidor
    [InlineData("https://shippingbox.duckdns.org.evil.com/app.apk", false)]               // host que solo empieza igual
    [InlineData("https://evil.com@shippingbox.duckdns.org/app.apk", false)]               // credenciales incrustadas
    [InlineData("ftp://shippingbox.duckdns.org/app.apk", false)]
    [InlineData("file:///sdcard/app.apk", false)]
    [InlineData("/descargas/app.apk", false)]                                             // no absoluta
    [InlineData("", false)]
    [InlineData(null, false)]
    public void LaUrlDelApk_SoloSeAceptaHttpsYDeNuestroServidor(string? url, bool esperado)
        => Assert.Equal(esperado, PoliticaDeActualizacion.EsUrlApkPermitida(url));

    [Theory]
    [InlineData(ShaValido, true)]
    [InlineData("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", true)]
    [InlineData("ba78-16bf-8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", true)]   // guiones ignorados
    [InlineData("...", false)]
    [InlineData("abc123", false)]
    [InlineData("zz7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", false)]    // no hexadecimal
    [InlineData("", false)]
    [InlineData(null, false)]
    public void LaHuella_DebeSerUnSha256Valido(string? sha, bool esperado)
        => Assert.Equal(esperado, PoliticaDeActualizacion.EsSha256Valido(sha));

    [Fact]
    public void SinHuellaEsperadaNoHayCoincidencia_AntesSeInstalabaSinVerificar()
    {
        Assert.False(PoliticaDeActualizacion.Sha256Coincide(null, ShaValido));
        Assert.False(PoliticaDeActualizacion.Sha256Coincide("", ShaValido));
        Assert.False(PoliticaDeActualizacion.Sha256Coincide("...", ShaValido));
        Assert.False(PoliticaDeActualizacion.Sha256Coincide(ShaValido, "0000000000000000000000000000000000000000000000000000000000000000"));
        Assert.True(PoliticaDeActualizacion.Sha256Coincide(ShaValido.ToUpperInvariant(), ShaValido));
    }

    [Fact]
    public async Task ElSha256Calculado_CoincideConElEsperadoParaUnContenidoConocido()
    {
        // SHA-256("abc") es el vector de prueba estándar
        using var flujo = new MemoryStream(Encoding.ASCII.GetBytes("abc"));

        var calculado = await PoliticaDeActualizacion.CalcularSha256Async(flujo);

        Assert.True(PoliticaDeActualizacion.Sha256Coincide(ShaValido, calculado));
    }

    [Theory]
    [InlineData(null, 1234, true)]       // el manifiesto no declara tamaño
    [InlineData(0, 1234, true)]
    [InlineData(1234, 1234, true)]
    [InlineData(1234, 1233, false)]      // descarga truncada
    [InlineData(1234, 5000, false)]
    public void ElTamanoDescargado_DebeCoincidirConElDelManifiestoSiLoDeclara(int? esperado, long real, bool coincide)
        => Assert.Equal(coincide, PoliticaDeActualizacion.TamanoCoincide(esperado, real));

    [Fact]
    public void ElManifiestoQuePublicaElScript_UsaLosNombresQueLeeLaApp()
    {
        // publicar-apk.ps1 escribe version.json con estos campos; la app los deserializa sin distinguir mayúsculas. "tamano" (sin ñ) no coincidía con la
        // propiedad Size de la app y el tamaño nunca se leía
        var script = File.ReadAllText(Path.Combine(Raiz(), "scripts/publicar-apk.ps1"));
        var servicio = File.ReadAllText(Path.Combine(Raiz(), "src/Mobile/UltimaMilla.Mobile/Services/Updates/AppUpdateService.cs"));
        var dto = servicio[servicio.IndexOf("record VersionManifiestoDto", StringComparison.Ordinal)..servicio.IndexOf("record ResultadoVerificacionActualizacion", StringComparison.Ordinal)];

        var camposDelScript = Regex.Matches(script[script.IndexOf("$datos = [ordered]@{", StringComparison.Ordinal)..], @"^\s+(\w+)\s+=", RegexOptions.Multiline)
            .Take(6).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(new[] { "versionCode", "versionName", "apkUrl", "sha256", "tamano", "notas" }, camposDelScript);

        foreach (var campo in new[] { "versionCode", "versionName", "apkUrl", "sha256", "notas" })
            Assert.Matches($@"(?i)\b{campo}\b", dto);
        Assert.Contains("JsonPropertyName(\"tamano\")", dto);
    }

    [Fact]
    public void ElServicioDeActualizacion_NoInstalaSinVerificarNiAceptaOtroServidor()
    {
        var servicio = File.ReadAllText(Path.Combine(Raiz(), "src/Mobile/UltimaMilla.Mobile/Services/Updates/AppUpdateService.cs"));

        Assert.Contains("PoliticaDeActualizacion.EsUrlApkPermitida", servicio);
        Assert.Contains("PoliticaDeActualizacion.EsSha256Valido", servicio);
        Assert.Contains("PoliticaDeActualizacion.Sha256Coincide", servicio);
        Assert.Contains("PoliticaDeActualizacion.TamanoCoincide", servicio);
        Assert.Contains("AllowAutoRedirect = false", servicio);
        Assert.DoesNotContain("\"...\"", servicio);                     // la excepción que dejaba pasar una huella inválida
        Assert.DoesNotContain("!string.IsNullOrWhiteSpace(sha256Esperado) &&", servicio);   // verificación opcional: ahora es obligatoria
    }
}
