using UltimaMilla.Mobile.Responsive;
using Xunit;

namespace UltimaMilla.Frontend.Tests.Responsive;

public class ReglasResponsiveTests
{
    // =========================================================================
    // 1. EsCompacto - Detección de dispositivos compactos y casos límite
    // =========================================================================

    [Theory]
    [InlineData(320d)]   // iPhone SE (1ra gen)
    [InlineData(360d)]   // Android chico típico (Galaxy A0x / Moto E)
    [InlineData(375d)]   // iPhone 8 / SE (2da/3ra gen)
    [InlineData(379.9d)] // Límite inferior inmediato del umbral
    public void EsCompacto_ConAnchoMenorAUmbral_DebeRetornarTrue(double ancho)
    {
        var resultado = ReglasResponsive.EsCompacto(ancho);
        Assert.True(resultado);
    }

    [Theory]
    [InlineData(380d)]   // Umbral exacto
    [InlineData(393d)]   // Teléfono estándar moderno (iPhone 15 / Pixel 7)
    [InlineData(412d)]   // Teléfono grande (Galaxy S23/S24)
    [InlineData(768d)]   // Tablet
    [InlineData(1280d)]  // Desktop / Monitor
    public void EsCompacto_ConAnchoIgualOMayorAUmbral_DebeRetornarFalse(double ancho)
    {
        var resultado = ReglasResponsive.EsCompacto(ancho);
        Assert.False(resultado);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(-500d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void EsCompacto_ConValoresAnomalos_DebeRetornarFalseSinLanzarExcepcion(double valorInvalido)
    {
        var resultado = ReglasResponsive.EsCompacto(valorInvalido);
        Assert.False(resultado);
    }

    // =========================================================================
    // 2. CabeEnFila - Decisión de apilamiento adaptativo y resiliencia
    // =========================================================================

    [Fact]
    public void CabeEnFila_ConListaNula_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ReglasResponsive.CabeEnFila(300d, null!, 8d));
    }

    [Fact]
    public void CabeEnFila_ConListaVaciaOUno_SiempreRetornaTrue()
    {
        Assert.True(ReglasResponsive.CabeEnFila(50d, Array.Empty<double>(), 8d));
        Assert.True(ReglasResponsive.CabeEnFila(50d, new[] { 100d }, 8d));
    }

    [Fact]
    public void CabeEnFila_ConAnchoInfinito_RetornaTrue()
    {
        var anchos = new[] { 100d, 150d, 200d };
        Assert.True(ReglasResponsive.CabeEnFila(double.PositiveInfinity, anchos, 10d));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-10d)]
    [InlineData(double.NaN)]
    public void CabeEnFila_ConAnchoDisponibleInvalidoYVariosElementos_RetornaFalse(double anchoInvalido)
    {
        var anchos = new[] { 50d, 50d };
        Assert.False(ReglasResponsive.CabeEnFila(anchoInvalido, anchos, 8d));
    }

    [Fact]
    public void CabeEnFila_CuandoSumaMasEspaciadoEsMenorOIgualAlDisponible_RetornaTrue()
    {
        // 3 botones: 80 + 80 + 80 = 240, 2 espaciados de 10 = 20. Total = 260.
        var anchos = new[] { 80d, 80d, 80d };

        Assert.True(ReglasResponsive.CabeEnFila(260d, anchos, 10d)); // Justo exacto
        Assert.True(ReglasResponsive.CabeEnFila(300d, anchos, 10d)); // Con margen
    }

    [Fact]
    public void CabeEnFila_CuandoSumaMasEspaciadoSuperaDisponible_RetornaFalse()
    {
        // 3 botones en 250dp de ancho interior: no entran (requiere 260dp)
        var anchos = new[] { 80d, 80d, 80d };
        Assert.False(ReglasResponsive.CabeEnFila(250d, anchos, 10d));
    }

    [Fact]
    public void CabeEnFila_ConValoresNegativosOAnomalosEnDeseados_NormalizaSinFallar()
    {
        // Negativos se normalizan a 0: 0 + 50 = 50, espaciado 10 = total 60
        var anchos = new[] { -10d, 50d };
        Assert.True(ReglasResponsive.CabeEnFila(70d, anchos, 10d));
    }

    // =========================================================================
    // 3. DistribuirAnchos - Distribución armónica y escalado proporcional
    // =========================================================================

    [Fact]
    public void DistribuirAnchos_ConListaNula_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ReglasResponsive.DistribuirAnchos(300d, null!, 8d));
    }

    [Fact]
    public void DistribuirAnchos_ConListaVacia_RetornaColeccionVacia()
    {
        var resultado = ReglasResponsive.DistribuirAnchos(300d, Array.Empty<double>(), 8d);
        Assert.Empty(resultado);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-100d)]
    [InlineData(double.NaN)]
    public void DistribuirAnchos_ConAnchoInvalido_RetornaCerosSinLanzarExcepcion(double anchoInvalido)
    {
        var anchos = new[] { 100d, 100d };
        var resultado = ReglasResponsive.DistribuirAnchos(anchoInvalido, anchos, 8d);

        Assert.Equal(2, resultado.Length);
        Assert.All(resultado, w => Assert.Equal(0d, w));
    }

    [Fact]
    public void DistribuirAnchos_ConSobrante_ReparteEspacioEquitativamente()
    {
        // Disponible: 320. 2 elementos de 100 con espaciado 20.
        // Espacio útil = 320 - 20 = 300. Deseado total = 200. Sobrante = 100.
        // Cada elemento recibe: 100 + (100 / 2) = 150.
        var anchos = new[] { 100d, 100d };
        var resultado = ReglasResponsive.DistribuirAnchos(320d, anchos, 20d);

        Assert.Equal(2, resultado.Length);
        Assert.Equal(150d, resultado[0], 2);
        Assert.Equal(150d, resultado[1], 2);
        Assert.Equal(300d, resultado.Sum(), 2);
    }

    [Fact]
    public void DistribuirAnchos_SinEspacioSuficiente_EscalaProporcionalmente()
    {
        // Disponible: 110. 2 elementos de 100 con espaciado 10.
        // Espacio útil = 110 - 10 = 100. Deseado total = 200.
        // Factor = 100 / 200 = 0.5. Cada elemento = 50.
        var anchos = new[] { 100d, 100d };
        var resultado = ReglasResponsive.DistribuirAnchos(110d, anchos, 10d);

        Assert.Equal(2, resultado.Length);
        Assert.Equal(50d, resultado[0], 2);
        Assert.Equal(50d, resultado[1], 2);
        Assert.Equal(100d, resultado.Sum(), 2);
    }

    [Fact]
    public void DistribuirAnchos_ConDeseadosEnCero_ReparteEspacioUtilEquitativamente()
    {
        // Disponible: 100, 2 elementos de 0, espaciado 0.
        var anchos = new[] { 0d, 0d };
        var resultado = ReglasResponsive.DistribuirAnchos(100d, anchos, 0d);

        Assert.Equal(2, resultado.Length);
        Assert.Equal(50d, resultado[0]);
        Assert.Equal(50d, resultado[1]);
    }
}
