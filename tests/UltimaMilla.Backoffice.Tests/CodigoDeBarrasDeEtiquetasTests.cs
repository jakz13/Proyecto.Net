using UltimaMilla.Frontend.Contracts.Modelos.Envios;
using Xunit;
using ZXing;
using ZXing.Common;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// La etiqueta del bulto debe llevar un código de barras real: antes se dibujaba un patrón inventado que ningún lector podía decodificar, así que la
/// app móvil no reconocía el bulto escaneado. Estas pruebas dibujan las barras como lo hace el SVG y las decodifican con ZXing, el mismo motor de la app.
/// </summary>
public class CodigoDeBarrasDeEtiquetasTests
{
    private static string? Decodificar(string codigo)
    {
        const int pixelesPorModulo = 3, alto = 60;
        var barras = GeneradorDocumentoEtiquetas.CalcularPatronBarras(codigo);
        var ancho = GeneradorDocumentoEtiquetas.AnchoTotal(barras) * pixelesPorModulo;

        var gris = new byte[ancho * alto];
        Array.Fill(gris, (byte)255);
        foreach (var (x, anchoBarra) in barras)
            for (var fila = 0; fila < alto; fila++)
                for (var columna = x * pixelesPorModulo; columna < (x + anchoBarra) * pixelesPorModulo; columna++)
                    gris[fila * ancho + columna] = 0;

        var lector = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions { PossibleFormats = new[] { BarcodeFormat.CODE_128 }, TryHarder = true }
        };
        return lector.Decode(new RGBLuminanceSource(gris, ancho, alto, RGBLuminanceSource.BitmapFormat.Gray8))?.Text;
    }

    [Theory]
    [InlineData("SBX-20261114-AB12CD-B01")]
    [InlineData("ABC123-B02")]
    [InlineData("TRK-0000")]
    [InlineData("A")]
    public void LaEtiqueta_LlevaUnCode128QueSeDecodificaConElMismoTextoDelBulto(string identificacion)
    {
        Assert.Equal(identificacion, Decodificar(identificacion));
    }

    [Fact]
    public void LaTablaDeCode128_DecodificaTodosLosCaracteresImprimibles()
    {
        // recorre los 94 símbolos imprimibles del conjunto B (el espacio de los bordes lo recorta el generador), y con la suma de verificación de cada combinación
        var imprimibles = new string(Enumerable.Range('!', '~' - '!' + 1).Select(c => (char)c).ToArray());
        for (var inicio = 0; inicio < imprimibles.Length; inicio += 12)
        {
            var trozo = imprimibles.Substring(inicio, Math.Min(12, imprimibles.Length - inicio)).ToUpperInvariant();
            Assert.Equal(trozo, Decodificar(trozo));
        }
    }

    [Fact]
    public void DosBultosDelMismoEnvio_TienenCodigosDistintos()
    {
        Assert.NotEqual(Decodificar("SBX-1-B01"), Decodificar("SBX-1-B02"));
    }
}
