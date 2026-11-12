using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Tests;

/// <summary>Punto en polígono de las zonas de cobertura y cédula mínima del repartidor.</summary>
public class CoberturaYDocumentoRepartidorTests
{
    // Cuadrado de Montevideo Centro: latitud -34.92 a -34.90, longitud -56.20 a -56.15
    private const string Cuadrado = "[[-34.90, -56.20], [-34.90, -56.15], [-34.92, -56.15], [-34.92, -56.20], [-34.90, -56.20]]";

    private static ZonaCobertura Zona(string poligono) => new(Guid.NewGuid(), Guid.NewGuid(), "Centro", poligono);

    [Fact]
    public void Contiene_PuntoDentroDelPoligono_DevuelveVerdadero()
    {
        Assert.True(Zona(Cuadrado).Contiene(-34.91, -56.17));
    }

    [Theory]
    [InlineData(-34.80, -56.17)]
    [InlineData(-34.91, -56.00)]
    [InlineData(-34.95, -56.30)]
    public void Contiene_PuntoFueraDelPoligono_DevuelveFalso(double lat, double lng)
    {
        Assert.False(Zona(Cuadrado).Contiene(lat, lng));
    }

    [Fact]
    public void Contiene_GeoJson_UsaElOrdenLongitudLatitud()
    {
        var geoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[-56.20,-34.90],[-56.15,-34.90],[-56.15,-34.92],[-56.20,-34.92],[-56.20,-34.90]]]}";
        var zona = Zona(geoJson);

        Assert.True(zona.Contiene(-34.91, -56.17));
        Assert.False(zona.Contiene(-34.80, -56.17));
    }

    [Fact]
    public void Contiene_PoligonoIlegible_NoContieneNada()
    {
        Assert.False(Zona("Montevideo Centro").Contiene(-34.91, -56.17));
    }

    [Theory]
    [InlineData("4.111.222-3")]
    [InlineData("41112223")]
    [InlineData("CI 4.321.987-6")]
    public void Repartidor_CedulaConAlMenosOchoDigitos_SeAcepta(string documento)
    {
        var repartidor = new Repartidor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), documento, "099111222");
        Assert.Equal(documento, repartidor.Documento);
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("1.234.56")]
    [InlineData("abc")]
    public void Repartidor_CedulaConMenosDeOchoDigitos_SeRechaza(string documento)
    {
        Assert.Throws<DomainValidationException>(() =>
            new Repartidor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), documento, "099111222"));
    }

    [Fact]
    public void Repartidor_ActualizarDocumentoConMenosDeOchoDigitos_SeRechazaYConservaElAnterior()
    {
        var repartidor = new Repartidor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "4.111.222-3", "099111222");

        Assert.Throws<DomainValidationException>(() => repartidor.ActualizarDocumento("123456"));
        Assert.Equal("4.111.222-3", repartidor.Documento);
    }
}
