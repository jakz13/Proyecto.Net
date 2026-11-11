using UltimaMilla.Application.Common;

namespace UltimaMilla.Application.Tests;

public class PoliticaDePasswordTests
{
    [Theory]
    [InlineData("Clave.Segura123")]
    [InlineData("correcto-caballo-bateria-9")]
    [InlineData("Mayusculas2026Y")]
    public void UnaContrasenaLargaYVariada_SeAcepta(string password)
    {
        Assert.Null(PoliticaDePassword.Validar(password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnaContrasenaVacia_SeRechaza(string? password)
    {
        Assert.NotNull(PoliticaDePassword.Validar(password));
    }

    [Fact]
    public void UnaContrasenaCorta_SeRechazaAunqueSeaVariada()
    {
        Assert.Contains("al menos 10", PoliticaDePassword.Validar("Ab1.xyz")!);
    }

    [Theory]
    [InlineData("soloMinusculasLargas")]   // una sola clase de caracteres
    [InlineData("1234567890123")]          // solo números
    [InlineData("MAYUSCULAS12345")]        // dos clases
    public void UnaContrasenaDeUnaOLasDosClases_SeRechaza(string password)
    {
        Assert.NotNull(PoliticaDePassword.Validar(password));
    }

    [Theory]
    [InlineData("Password123")]
    [InlineData("aaaaaaAAA111")]            // pocos caracteres distintos
    public void UnaContrasenaTrivial_SeRechaza(string password)
    {
        Assert.NotNull(PoliticaDePassword.Validar(password));
    }
}
