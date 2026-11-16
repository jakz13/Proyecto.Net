using UltimaMilla.Backoffice.Pages.Zonas;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>La opción "Todos los días" de una franja horaria se convierte en una franja por cada día de la semana.</summary>
public class FranjasTodosLosDiasTests
{
    [Fact]
    public void TodosLosDias_SeExpandeEnSieteFranjasIguales()
    {
        var franja = new CrearFranjaHorariaDto { DiaSemana = FranjasTodosLosDias.Valor, HoraInicio = new TimeOnly(9, 0), HoraFin = new TimeOnly(13, 0), CapacidadMaximaEnvios = 20 };

        var resultado = FranjasTodosLosDias.Expandir(new[] { franja });

        Assert.Equal(7, resultado.Count);
        Assert.Equal(Enum.GetValues<DayOfWeek>().OrderBy(d => d), resultado.Select(f => f.DiaSemana).OrderBy(d => d));
        Assert.All(resultado, f => { Assert.Equal(new TimeOnly(9, 0), f.HoraInicio); Assert.Equal(20, f.CapacidadMaximaEnvios); });
    }

    [Fact]
    public void UnDiaConcreto_NoSeToca()
    {
        var franja = new CrearFranjaHorariaDto { DiaSemana = DayOfWeek.Friday };

        var resultado = FranjasTodosLosDias.Expandir(new[] { franja });

        Assert.Single(resultado);
        Assert.Equal(DayOfWeek.Friday, resultado[0].DiaSemana);
    }
}
