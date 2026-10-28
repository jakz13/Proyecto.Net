using UltimaMilla.Application.Tablero;

namespace UltimaMilla.Application.Tests;

public class EvaluadorRiesgoVentanaTests
{
    private static readonly DateTime Ahora = new(2026, 10, 28, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LlegadaConMargenSuficiente_NoEsRiesgo()
    {
        var limite = Ahora.AddHours(2);

        Assert.Null(EvaluadorRiesgoVentana.Evaluar(Ahora, limite, Ahora.AddMinutes(30)));
    }

    [Theory]
    [InlineData(5, NivelRiesgo.Moderado)]
    [InlineData(10, NivelRiesgo.Alto)]
    [InlineData(29, NivelRiesgo.Alto)]
    [InlineData(30, NivelRiesgo.Critico)]
    [InlineData(90, NivelRiesgo.Critico)]
    public void LlegadaPosteriorAlLimite_SeClasificaPorMinutosDeRetraso(int minutosTarde, NivelRiesgo esperado)
    {
        var limite = Ahora.AddMinutes(60);

        var riesgo = EvaluadorRiesgoVentana.Evaluar(Ahora, limite, limite.AddMinutes(minutosTarde));

        Assert.NotNull(riesgo);
        Assert.Equal(esperado, riesgo!.Nivel);
        Assert.Equal(minutosTarde, riesgo.MinutosRetraso);
    }

    [Fact]
    public void LlegadaConMenosDelMargenPreventivo_EsRiesgoModeradoSinRetraso()
    {
        var limite = Ahora.AddMinutes(60);

        var riesgo = EvaluadorRiesgoVentana.Evaluar(Ahora, limite, limite.AddMinutes(-EvaluadorRiesgoVentana.MargenPreventivoMinutos + 1));

        Assert.Equal(NivelRiesgo.Moderado, riesgo!.Nivel);
        Assert.Equal(0, riesgo.MinutosRetraso);
    }

    [Fact]
    public void LlegadaJustoConElMargenPreventivo_NoEsRiesgo()
    {
        var limite = Ahora.AddMinutes(60);

        Assert.Null(EvaluadorRiesgoVentana.Evaluar(Ahora, limite, limite.AddMinutes(-EvaluadorRiesgoVentana.MargenPreventivoMinutos)));
    }

    [Fact]
    public void VentanaYaVencida_EsCriticoAunqueLaLlegadaSeaInmediata()
    {
        var limite = Ahora.AddMinutes(-5);

        var riesgo = EvaluadorRiesgoVentana.Evaluar(Ahora, limite, Ahora.AddMinutes(1));

        Assert.Equal(NivelRiesgo.Critico, riesgo!.Nivel);
        Assert.Contains("venció", riesgo.Causa);
    }

    [Fact]
    public void IncidenciaAbierta_SeMencionaEnLaCausa()
    {
        var limite = Ahora.AddMinutes(60);

        var riesgo = EvaluadorRiesgoVentana.Evaluar(Ahora, limite, limite.AddMinutes(40), "AveriaVehiculo");

        Assert.Contains("AveriaVehiculo", riesgo!.Causa);
    }

    [Fact]
    public void Estimacion_SinParadasDevuelveAhora()
    {
        Assert.Equal(Ahora, EvaluadorRiesgoVentana.EstimarLlegada(Ahora, null, Array.Empty<PuntoRuta>()));
    }

    [Fact]
    public void Estimacion_SinCoordenadasUsaElTiempoFijoDeTramoYDeServicio()
    {
        var recorrido = new[] { new PuntoRuta(null, null), new PuntoRuta(null, null), new PuntoRuta(null, null) };

        var llegada = EvaluadorRiesgoVentana.EstimarLlegada(Ahora, null, recorrido);

        // 3 tramos sin coordenadas + servicio en las 2 paradas previas a la evaluada
        var esperado = 3 * EvaluadorRiesgoVentana.MinutosTramoSinCoordenadas + 2 * EvaluadorRiesgoVentana.MinutosPorParada;
        Assert.Equal(Ahora.AddMinutes(esperado), llegada);
    }

    [Fact]
    public void Estimacion_ConCoordenadasDependeDeLaDistancia()
    {
        var origen = new PuntoRuta(-34.9011, -56.1645);
        var cerca = new[] { new PuntoRuta(-34.9050, -56.1645) };
        var lejos = new[] { new PuntoRuta(-34.7500, -56.1645) };

        var llegadaCerca = EvaluadorRiesgoVentana.EstimarLlegada(Ahora, origen, cerca);
        var llegadaLejos = EvaluadorRiesgoVentana.EstimarLlegada(Ahora, origen, lejos);

        Assert.True(llegadaCerca < llegadaLejos);
        Assert.True(llegadaCerca > Ahora);
    }

    [Fact]
    public void Distancia_EntreMontevideoYCanelonesEsCoherente()
    {
        // Plaza Independencia (Montevideo) a Las Piedras: casi 20 km en línea recta
        var km = EvaluadorRiesgoVentana.DistanciaKm(-34.9060, -56.1990, -34.7300, -56.2200);

        Assert.InRange(km, 18, 21);
    }
}
