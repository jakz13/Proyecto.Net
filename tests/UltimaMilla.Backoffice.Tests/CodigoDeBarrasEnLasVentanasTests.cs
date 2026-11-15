using System.Reflection;
using System.Text.RegularExpressions;
using UltimaMilla.Frontend.Contracts.Modelos.Envios;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// Las ventanas "Etiquetas y Códigos de Barras" del Backoffice (envíos y hoja de ruta) dibujan las barras en JavaScript. Antes tenían su propia copia del
/// patrón inventado, distinta de la etiqueta impresa y no legible por ningún lector (visto en una captura del 14/11). Ahora usan <c>code128.js</c>,
/// que debe seguir siendo el mismo Code 128 que genera el C# (cuya decodificación con ZXing prueba <see cref="CodigoDeBarrasDeEtiquetasTests"/>).
/// </summary>
public class CodigoDeBarrasEnLasVentanasTests
{
    private static string Raiz()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.prod.yml")))
            carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);
        return carpeta!.FullName;
    }

    [Fact]
    public void LaTablaDeCode128EnJavaScript_EsLaMismaQueLaDelGeneradorEnCSharp()
    {
        var js = File.ReadAllText(Path.Combine(Raiz(), "src/Web/Backoffice/wwwroot/js/code128.js"));
        var tablaJs = Regex.Matches(js[js.IndexOf("PATRONES", StringComparison.Ordinal)..js.IndexOf("INICIO_B", StringComparison.Ordinal)], @"""(\d{6,7})""")
            .Select(m => m.Groups[1].Value).ToArray();

        var campo = typeof(GeneradorDocumentoEtiquetas).GetField("PatronesCode128", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(campo);
        var tablaCs = (string[])campo!.GetValue(null)!;

        Assert.Equal(107, tablaJs.Length);
        Assert.Equal(tablaCs, tablaJs);
    }

    [Theory]
    [InlineData("src/Web/Backoffice/Pages/Envios/Index.cshtml")]
    [InlineData("src/Web/Backoffice/Pages/Rutas/Detalle.cshtml")]
    public void LasVentanasDeEtiquetas_UsanCode128YNoUnPatronPropio(string pagina)
    {
        var html = File.ReadAllText(Path.Combine(Raiz(), pagina));

        Assert.Contains("~/js/code128.js", html);
        Assert.Contains("Code128.svg(", html);
        Assert.DoesNotContain("valor % 3", html);
    }
}
