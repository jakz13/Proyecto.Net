using UltimaMilla.Domain.Entities;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;
using Xunit;

namespace UltimaMilla.Domain.Tests;

public class EnvioTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CrearEnvio_ConNombreDestinatarioVacio_LanzaDomainValidationException(string? nombreInvalido)
    {
        // Arrange
        var id = Guid.NewGuid();
        var comercioId = Guid.NewGuid();
        var direccion = "Av. 18 de Julio 1234, Montevideo";

        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() =>
            new Envio(id, comercioId, direccion, nombreInvalido!));

        Assert.Contains("nombre del destinatario", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CrearEnvio_ConDireccionDestinoVacia_LanzaDomainValidationException(string? direccionInvalida)
    {
        // Arrange
        var id = Guid.NewGuid();
        var comercioId = Guid.NewGuid();
        var nombre = "Juan Pérez";

        // Act & Assert
        var exception = Assert.Throws<DomainValidationException>(() =>
            new Envio(id, comercioId, direccionInvalida!, nombre));

        Assert.Contains("dirección de destino", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CrearEnvio_ConDatosValidos_InicializaPropiedadesCorrectamente()
    {
        // Arrange
        var id = Guid.NewGuid();
        var comercioId = Guid.NewGuid();
        var direccion = "Rambla Gandhi 456, Montevideo";
        var destinatario = "María García";
        var antesDeCrear = DateTime.UtcNow;

        // Act
        var envio = new Envio(id, comercioId, direccion, destinatario);

        // Assert
        Assert.Equal(id, envio.Id);
        Assert.Equal(comercioId, envio.ComercioId);
        Assert.Equal(direccion, envio.DireccionDestino);
        Assert.Equal(destinatario, envio.NombreDestinatario);
        Assert.Equal(EstadoEnvio.Admitido, envio.Estado);
        Assert.True(envio.FechaCreacion >= antesDeCrear && envio.FechaCreacion <= DateTime.UtcNow);
    }
}
