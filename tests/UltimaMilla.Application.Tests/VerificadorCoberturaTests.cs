using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Services;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

/// <summary>Un envío solo se admite si la dirección cae dentro de una zona activa del operador.</summary>
public class VerificadorCoberturaTests
{
    private const string Cuadrado = "[[-34.90, -56.20], [-34.90, -56.15], [-34.92, -56.15], [-34.92, -56.20], [-34.90, -56.20]]";

    private readonly Guid _operador = Guid.NewGuid();
    private readonly FakeZonaCoberturaRepository _zonas = new();
    private readonly GeocodificadorFalso _geocodificador = new();
    private readonly ZonaCobertura _centro;

    public VerificadorCoberturaTests()
    {
        _centro = new ZonaCobertura(Guid.NewGuid(), _operador, "Centro", Cuadrado);
        _zonas.Zonas.Add(_centro);
    }

    private VerificadorCobertura Verificador(bool validar = true) =>
        new(_zonas, _geocodificador, new OpcionesCobertura { Validar = validar });

    [Fact]
    public async Task DireccionDentroDeUnaZona_SeAdmiteYDevuelveLaZona()
    {
        _geocodificador.Resultado = (-34.91, -56.17);

        var cobertura = await Verificador().VerificarAsync(_operador, "18 de Julio", "1000", "Montevideo", null, null, null);

        Assert.Equal(_centro.Id, cobertura.ZonaCoberturaId);
        Assert.Equal(-34.91, cobertura.Latitud);
    }

    [Fact]
    public async Task DireccionFueraDeTodasLasZonas_SeRechaza()
    {
        _geocodificador.Resultado = (-34.60, -56.00);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            Verificador().VerificarAsync(_operador, "Ruta 8", "5000", "Pando", null, null, null));

        Assert.Contains("fuera de las zonas de cobertura", ex.Message);
    }

    [Fact]
    public async Task DireccionQueNoSePuedeUbicar_SeRechaza()
    {
        _geocodificador.Resultado = null;

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Verificador().VerificarAsync(_operador, "Calle inexistente", "1", "Montevideo", null, null, null));
    }

    [Fact]
    public async Task GeocodificadorCaido_SeRechazaConUnMensajeDeReintento()
    {
        _geocodificador.Falla = true;

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            Verificador().VerificarAsync(_operador, "18 de Julio", "1000", "Montevideo", null, null, null));

        Assert.Contains("Intente nuevamente", ex.Message);
    }

    [Fact]
    public async Task ConCoordenadasInformadas_NoSeGeocodificaYSeVerificaElPoligono()
    {
        _geocodificador.Falla = true;

        var cobertura = await Verificador().VerificarAsync(_operador, "X", "1", "Montevideo", null, -34.91, -56.17);
        Assert.Equal(_centro.Id, cobertura.ZonaCoberturaId);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Verificador().VerificarAsync(_operador, "X", "1", "Montevideo", null, -34.50, -56.00));
    }

    [Fact]
    public async Task ZonaIndicadaInactivaOAjena_SeRechaza()
    {
        _centro.Desactivar();
        var ajena = new ZonaCobertura(Guid.NewGuid(), Guid.NewGuid(), "Otra", Cuadrado);
        _zonas.Zonas.Add(ajena);
        var activa = new ZonaCobertura(Guid.NewGuid(), _operador, "Costa", Cuadrado);
        _zonas.Zonas.Add(activa);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Verificador().VerificarAsync(_operador, "X", "1", "Montevideo", _centro.Id, null, null));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Verificador().VerificarAsync(_operador, "X", "1", "Montevideo", ajena.Id, null, null));

        var ok = await Verificador().VerificarAsync(_operador, "X", "1", "Montevideo", activa.Id, null, null);
        Assert.Equal(activa.Id, ok.ZonaCoberturaId);
    }

    [Fact]
    public async Task OperadorSinZonasActivas_NoAdmiteEnvios()
    {
        _centro.Desactivar();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Verificador().VerificarAsync(_operador, "X", "1", "Montevideo", null, -34.91, -56.17));
    }

    [Fact]
    public async Task ZonasDeOtroOperador_NoCuentan()
    {
        _geocodificador.Resultado = (-34.91, -56.17);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Verificador().VerificarAsync(Guid.NewGuid(), "X", "1", "Montevideo", null, null, null));
    }

    [Fact]
    public async Task ConLaValidacionApagada_NoSeToca_NiSeGeocodifica()
    {
        _geocodificador.Falla = true;

        var cobertura = await Verificador(validar: false).VerificarAsync(_operador, "X", "1", "Montevideo", null, null, null);

        Assert.Null(cobertura.ZonaCoberturaId);
    }

    private sealed class GeocodificadorFalso : IGeocodificador
    {
        public (double, double)? Resultado { get; set; }
        public bool Falla { get; set; }

        public Task<(double Latitud, double Longitud)?> GeocodificarAsync(string calle, string numero, string ciudad, CancellationToken cancellationToken = default)
        {
            if (Falla) throw new HttpRequestException("sin servicio");
            return Task.FromResult<(double Latitud, double Longitud)?>(Resultado);
        }
    }
}
