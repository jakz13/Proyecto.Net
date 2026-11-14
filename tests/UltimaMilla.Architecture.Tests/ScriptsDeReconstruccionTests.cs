using Xunit;

namespace UltimaMilla.Architecture.Tests;

/// <summary>
/// El escenario 7 de la defensa destruye y reconstruye el ambiente con un comando: si un script de ese camino está vacío o no se invoca,
/// la infraestructura y la aplicación se recrean pero los dominios quedan apuntando a una IP que ya no existe (hallazgo D-16 de la auditoría del 14/11:
/// `actualizar-dns.ps1` estuvo vacío desde que se creó y la reconstrucción dejó la plataforma sin servicio casi cuatro horas).
/// </summary>
public class ScriptsDeReconstruccionTests
{
    private static string RaizDelRepositorio()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.prod.yml")))
            carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);
        return carpeta!.FullName;
    }

    private static string Leer(string rutaRelativa) => File.ReadAllText(Path.Combine(RaizDelRepositorio(), rutaRelativa));

    [Fact]
    public void ActualizarDns_NoEstaVacio_Y_ActualizaDuckDnsConElToken()
    {
        var script = Leer(Path.Combine("scripts", "actualizar-dns.ps1"));

        Assert.True(script.Trim().Length > 500, "scripts/actualizar-dns.ps1 está vacío o es un esqueleto: la reconstrucción no actualizaría el DNS.");
        Assert.Contains("duckdns.org/update", script);
        Assert.Contains("DUCKDNS_TOKEN", script);
        Assert.Contains("-Ip", script);
    }

    [Fact]
    public void Reconstruir_GuardaElSecretoVmHostSinPasarPorUnaTuberia()
    {
        // Una tubería de Windows PowerShell 5.1 puede anteponer una marca BOM al valor del secreto y el despliegue del pipeline no resolvía el servidor (D-17)
        var script = Leer(Path.Combine("scripts", "reconstruir.ps1"));

        Assert.Contains("gh secret set VM_HOST", script);
        Assert.Contains("--body", script);
        Assert.DoesNotContain("| gh secret set", script);
    }

    [Fact]
    public void Reconstruir_ActualizaElDnsAntesDeDesplegarLaAplicacion()
    {
        var script = Leer(Path.Combine("scripts", "reconstruir.ps1"));

        var posDns = script.IndexOf("actualizar-dns.ps1", StringComparison.Ordinal);
        var posDespliegue = script.IndexOf("desplegar.ps1", StringComparison.Ordinal);
        Assert.True(posDns >= 0, "reconstruir.ps1 no invoca actualizar-dns.ps1.");
        Assert.True(posDespliegue > posDns, "El DNS debe apuntar a la VM nueva antes de levantar Caddy: sin eso no puede obtener los certificados.");
    }
}
