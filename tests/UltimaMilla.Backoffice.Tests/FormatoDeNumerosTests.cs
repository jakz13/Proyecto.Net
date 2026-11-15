using UltimaMilla.Backoffice;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// El servidor corre con cultura invariante: "8500" se mostraba como "8,500" y "5.5" como "5.5", al revés de lo que lee un uruguayo (8,5 era "ocho coma cinco").
/// Todos los números de pantalla usan coma decimal y punto de miles; los atributos de datos que lee JavaScript siguen con punto decimal.
/// </summary>
public class FormatoDeNumerosTests
{
    [Theory]
    [InlineData(8500, 0, "8.500")]
    [InlineData(1234567, 0, "1.234.567")]
    [InlineData(5.5, 1, "5,5")]
    [InlineData(12.345, 3, "12,345")]
    [InlineData(0, 2, "0,00")]
    public void LosNumerosSeMuestranConComaDecimalYPuntoDeMiles(double valor, int decimales, string esperado)
    {
        Assert.Equal(esperado, Formato.N((decimal)valor, decimales));
        Assert.Equal(esperado, Formato.N(valor, decimales));
    }

    [Fact]
    public void LosAtributosDeDatosUsanPuntoDecimalSinSeparadorDeMiles()
    {
        Assert.Equal("8500", Formato.Dato(8500m));
        Assert.Equal("5.5", Formato.Dato(5.5m));
        Assert.Equal("12.345", Formato.Dato(12.345));
    }
}
