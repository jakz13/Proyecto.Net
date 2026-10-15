using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Tests;

public class AltaEnvioDominioTests
{
    private static Envio NuevoEnvio() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-ALTA0001");

    [Fact]
    public void Direccion_Valida_ArmaTextoYNormalizaEspacios()
    {
        var direccion = new Direccion(Guid.NewGuid(), " Rivera ", " 10 ", " Montevideo ");

        Assert.Equal("Rivera 10, Montevideo", direccion.ToString());
    }

    [Fact]
    public void Direccion_IdVacio_LanzaExcepcion()
    {
        Assert.Throws<DomainValidationException>(() => new Direccion(Guid.Empty, "Rivera", "10", "Montevideo"));
    }

    [Theory]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    public void Direccion_CoordenadasEnElLimite_SonValidas(double latitud, double longitud)
    {
        var direccion = new Direccion(Guid.NewGuid(), "Rivera", "10", "Montevideo", null, latitud, longitud);

        Assert.Equal(latitud, direccion.Latitud);
    }

    [Fact]
    public void Envio_AgregarBulto_SumaPesoYVolumen()
    {
        var envio = NuevoEnvio();

        envio.AgregarBulto("B1", 2m, 10m, 10m, 10m);
        envio.AgregarBulto("B2", 3m, 2m, 5m, 10m);

        Assert.Equal(5m, envio.PesoTotal);
        Assert.Equal(1100m, envio.VolumenTotal);
    }

    [Fact]
    public void Envio_BultoConIdentificacionRepetida_SeRechaza()
    {
        var envio = NuevoEnvio();
        envio.AgregarBulto("B1", 1m, 1m, 1m, 1m);

        Assert.Throws<DomainValidationException>(() => envio.AgregarBulto(" b1 ", 1m, 1m, 1m, 1m));
        Assert.Single(envio.Bultos);
    }

    [Fact]
    public void Envio_AgregarBultoFueraDeAdmitido_SeRechaza()
    {
        var envio = NuevoEnvio();
        envio.TransicionarEstado(EstadoEnvio.EnDeposito, "Recepcion");

        Assert.Throws<DomainValidationException>(() => envio.AgregarBulto("B1", 1m, 1m, 1m, 1m));
    }

    [Fact]
    public void Envio_ConstructorRico_ConservaCopiaDesnormalizadaDeDireccionYNombre()
    {
        var envio = new Envio(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "trk-x1",
            direccionDestino: " Rivera 10 ", nombreDestinatario: " Ana ");

        Assert.Equal("Rivera 10", envio.DireccionDestino);
        Assert.Equal("Ana", envio.NombreDestinatario);
        Assert.Equal("TRK-X1", envio.CodigoSeguimientoPublico);
    }

    [Fact]
    public void Cuadro_EstaVigenteEn_RespetaLosLimites()
    {
        var desde = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var hasta = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var cuadro = new CuadroTarifario(Guid.NewGuid(), Guid.NewGuid(), 1, desde, hasta);

        Assert.True(cuadro.EstaVigenteEn(desde));
        Assert.True(cuadro.EstaVigenteEn(hasta));
        Assert.False(cuadro.EstaVigenteEn(desde.AddTicks(-1)));
        Assert.False(cuadro.EstaVigenteEn(hasta.AddTicks(1)));
    }

    [Fact]
    public void Cuadro_SinFechaFin_EsVigenteHaciaElFuturo()
    {
        var cuadro = new CuadroTarifario(Guid.NewGuid(), Guid.NewGuid(), 1, DateTime.UtcNow.AddDays(-1));

        Assert.True(cuadro.EstaVigenteEn(DateTime.UtcNow.AddYears(50)));
    }

    [Fact]
    public void Cuadro_ObtenerReglaAplicable_SinReglasDevuelveNull()
    {
        var cuadro = new CuadroTarifario(Guid.NewGuid(), Guid.NewGuid(), 1, DateTime.UtcNow.AddDays(-1));

        Assert.Null(cuadro.ObtenerReglaAplicable(1m, 1m, "Estandar"));
    }

    [Theory]
    [InlineData(-1, 1, "Estandar")]
    [InlineData(1, -1, "Estandar")]
    [InlineData(1, 1, "")]
    [InlineData(1, 1, "  ")]
    public void Cuadro_ObtenerReglaAplicable_ParametrosInvalidos_LanzaExcepcion(decimal peso, decimal volumen, string modalidad)
    {
        var cuadro = new CuadroTarifario(Guid.NewGuid(), Guid.NewGuid(), 1, DateTime.UtcNow.AddDays(-1));

        Assert.Throws<DomainValidationException>(() => cuadro.ObtenerReglaAplicable(peso, volumen, modalidad));
    }

    [Fact]
    public void Cuadro_ObtenerReglaAplicable_PesoEnLosLimitesDelRango()
    {
        var cuadro = new CuadroTarifario(Guid.NewGuid(), Guid.NewGuid(), 1, DateTime.UtcNow.AddDays(-1));
        var regla = new ReglaTarifa(Guid.NewGuid(), cuadro.Id, "Estandar", 1m, 5m, 0m, 100m, 50m);
        cuadro.AgregarRegla(regla);

        Assert.Same(regla, cuadro.ObtenerReglaAplicable(1m, 10m, "Estandar"));
        Assert.Same(regla, cuadro.ObtenerReglaAplicable(5m, 10m, "Estandar"));
        Assert.Null(cuadro.ObtenerReglaAplicable(0.5m, 10m, "Estandar"));
        Assert.Null(cuadro.ObtenerReglaAplicable(5.01m, 10m, "Estandar"));
        Assert.Null(cuadro.ObtenerReglaAplicable(3m, 100.01m, "Estandar"));
    }
}
