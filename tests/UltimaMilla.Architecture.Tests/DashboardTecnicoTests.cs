using System.Text.Json;
using Xunit;

namespace UltimaMilla.Architecture.Tests;

/// <summary>
/// Reglas del tablero técnico de Grafana (letra 6.12) que ninguna prueba de código puede ver: el JSON se carga solo al desplegar,
/// y un panel mal armado compila, pasa el pipeline y recién se nota mirando el panel con el servidor ya en uso.
/// </summary>
public class DashboardTecnicoTests
{
    private static JsonElement LeerDashboard()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker", "grafana", "dashboards", "ultima-milla-tecnico.json")))
            carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);
        var ruta = Path.Combine(carpeta!.FullName, "docker", "grafana", "dashboards", "ultima-milla-tecnico.json");
        return JsonDocument.Parse(File.ReadAllText(ruta)).RootElement.Clone();
    }

    [Fact]
    public void ElPanelDeIntentosDeAviso_MideEventosPorIntervaloYNoElContadorCrudo()
    {
        // Los contadores viven en la memoria de cada proceso y vuelven a 0 al reiniciar (cada despliegue). Con la suma cruda el panel mostraba
        // los errores hasta el reinicio y después 0, como si nunca hubieran ocurrido: increase() los absorbe y conserva lo ocurrido antes.
        var panel = LeerDashboard().GetProperty("panels").EnumerateArray()
            .Single(p => p.GetProperty("title").GetString()!.Contains("intentos de aviso", StringComparison.OrdinalIgnoreCase));

        var expresiones = panel.GetProperty("targets").EnumerateArray().Select(t => t.GetProperty("expr").GetString()!).ToList();

        Assert.Contains(expresiones, e => e.Contains("um_avisos_comercio_intentos_total"));
        Assert.All(expresiones, e => Assert.True(
            e.Contains("increase(", StringComparison.Ordinal) || e.Contains("rate(", StringComparison.Ordinal),
            $"El panel suma un contador crudo, que se pone en 0 al reiniciar: {e}"));
    }
}
