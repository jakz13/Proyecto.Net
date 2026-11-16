using UltimaMilla.Domain.Entities.Operador;
using Xunit;

namespace UltimaMilla.Domain.Tests;

/// <summary>Un envío Express se tarifica con la regla "Urgente" del cuadro: antes no encontraba tarifa y el alta fallaba.</summary>
public class ModalidadTarifaExpressTests
{
    private static CuadroTarifario Cuadro()
    {
        var cuadro = new CuadroTarifario(Guid.NewGuid(), Guid.NewGuid(), 1, DateTime.UtcNow.AddDays(-1));
        cuadro.AgregarRegla(new ReglaTarifa(Guid.NewGuid(), cuadro.Id, "Estandar", 0m, 1000m, 0m, 1000m, 150m));
        cuadro.AgregarRegla(new ReglaTarifa(Guid.NewGuid(), cuadro.Id, "Urgente", 0m, 1000m, 0m, 1000m, 300m));
        return cuadro;
    }

    [Theory]
    [InlineData("Express")]
    [InlineData("express")]
    [InlineData("Urgente")]
    public void Express_UsaLaTarifaUrgente(string modalidad)
    {
        var regla = Cuadro().ObtenerReglaAplicable(2m, 0.01m, modalidad);

        Assert.NotNull(regla);
        Assert.Equal(300m, regla!.CalcularPrecioFinal());
    }

    [Fact]
    public void Estandar_NoSeConfundeConExpress()
    {
        var regla = Cuadro().ObtenerReglaAplicable(2m, 0.01m, "Estandar");

        Assert.Equal(150m, regla!.CalcularPrecioFinal());
    }
}
