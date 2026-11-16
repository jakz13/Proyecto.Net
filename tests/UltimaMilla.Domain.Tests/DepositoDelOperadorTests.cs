using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Tests;

public class DepositoDelOperadorTests
{
    private static IdentidadVisual Nueva() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "#1D4ED8", "#F97316", "contacto@operador.uy", "099001122");

    [Fact]
    public void SinDireccionConfigurada_ElOperadorNoTieneDeposito()
    {
        var identidad = Nueva();

        Assert.Null(identidad.DireccionDeposito);
        Assert.Null(identidad.DepositoLatitud);
        Assert.Null(identidad.DepositoLongitud);
    }

    [Fact]
    public void LaDireccionDelDepositoSeCambiaConSuUbicacion_YSeLimpiaSiSeVacia()
    {
        var identidad = Nueva();

        identidad.ActualizarDeposito("  Av. Italia 3456, Montevideo ", -34.8941, -56.1389);
        Assert.Equal("Av. Italia 3456, Montevideo", identidad.DireccionDeposito);
        Assert.Equal(-34.8941, identidad.DepositoLatitud);

        identidad.ActualizarDeposito("Ruta 8 km 17, Canelones", -34.7, -56.0);
        Assert.Equal("Ruta 8 km 17, Canelones", identidad.DireccionDeposito);

        identidad.ActualizarDeposito("   ", null, null);
        Assert.Null(identidad.DireccionDeposito);
        Assert.Null(identidad.DepositoLongitud);
    }

    [Theory]
    [InlineData(-34.9, null)]        // falta la longitud
    [InlineData(95.0, -56.1)]        // latitud fuera de rango
    [InlineData(-34.9, 200.0)]       // longitud fuera de rango
    public void UnaUbicacionIncompletaOFueraDeRangoSeRechaza(double? latitud, double? longitud)
    {
        Assert.Throws<DomainValidationException>(() => Nueva().ActualizarDeposito("Calle 1", latitud, longitud));
    }

    [Fact]
    public void UnaDireccionDemasiadoLargaSeRechaza()
    {
        Assert.Throws<DomainValidationException>(() => Nueva().ActualizarDeposito(new string('x', 301), -34.9, -56.1));
    }
}
