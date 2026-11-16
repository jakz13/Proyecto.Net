using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Exceptions;
using Xunit;

namespace UltimaMilla.Domain.Tests;

/// <summary>La cédula son exactamente ocho dígitos, solo números: sin puntos, guiones, letras ni espacios de más.</summary>
public class CedulaTests
{
    [Theory]
    [InlineData("12345678", true)]
    [InlineData("00123456", true)]
    [InlineData(" 12345678 ", true)]
    [InlineData("1234567", false)]
    [InlineData("123456789", false)]
    [InlineData("1.234.567-8", false)]
    [InlineData("1234567a", false)]
    [InlineData("CI 12345678", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EsValida_SoloOchoDigitosNumericos(string? cedula, bool esperado) =>
        Assert.Equal(esperado, Cedula.EsValida(cedula));

    [Fact]
    public void Destinatario_ConCedulaValidaOSinCedula_SeAcepta()
    {
        Assert.Equal("12345678", new Destinatario(Guid.NewGuid(), "Ana", "12345678").Documento);
        Assert.Null(new Destinatario(Guid.NewGuid(), "Ana").Documento);
    }

    [Theory]
    [InlineData("1.234.567-8")]
    [InlineData("1234567")]
    [InlineData("abcdefgh")]
    public void Destinatario_ConCedulaMalFormada_SeRechaza(string cedula) =>
        Assert.Throws<DomainValidationException>(() => new Destinatario(Guid.NewGuid(), "Ana", cedula));
}
