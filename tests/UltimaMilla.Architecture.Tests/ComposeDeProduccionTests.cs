using System.Text.RegularExpressions;
using Xunit;

namespace UltimaMilla.Architecture.Tests;

/// <summary>
/// Reglas de configuración del despliegue que ninguna prueba de código puede ver: si el compose de producción no las cumple, el sistema
/// compila y pasa todo el pipeline, pero falla en el servidor (auditoría del 14/11).
/// </summary>
public class ComposeDeProduccionTests
{
    private static string LeerCompose()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.prod.yml")))
            carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);
        return File.ReadAllText(Path.Combine(carpeta!.FullName, "docker-compose.prod.yml"));
    }

    /// <summary>Texto del servicio: desde su línea "  nombre:" hasta el siguiente servicio (dos espacios de sangría) o el final.</summary>
    private static string Servicio(string compose, string nombre)
    {
        var m = Regex.Match(compose, $@"(?ms)^  {Regex.Escape(nombre)}:\s*$(.*?)(?=^  [A-Za-z0-9_-]+:\s*$|^[A-Za-z]|\z)");
        Assert.True(m.Success, $"No se encontró el servicio '{nombre}' en docker-compose.prod.yml");
        return m.Groups[1].Value;
    }

    [Fact]
    public void ElWorker_PermiteEntregarAvisosAlReceptorDePruebaInterno()
    {
        // Los avisos a los comercios los entrega el worker, y la protección anti-SSRF bloquea las direcciones internas salvo las de esta lista.
        // La excepción estaba en el Backoffice (que no envía avisos) y no en el worker: el receptor de prueba del escenario 6 nunca recibía nada (D-11).
        var worker = Servicio(LeerCompose(), "worker");

        var linea = Regex.Match(worker, @"Avisos__HostsPermitidos:\s*""?([^""\r\n]+)""?");
        Assert.True(linea.Success, "El worker no define Avisos__HostsPermitidos.");
        var hosts = linea.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("api1", hosts);
        Assert.Contains("api2", hosts);
    }

    [Fact]
    public void TodosLosServicios_RotanSusRegistros()
    {
        // El driver json-file de Docker no tiene límites por defecto: los registros crecen sin tope y con las semanas llenan el disco de la VM (D-18)
        var compose = LeerCompose();

        Assert.Matches(@"x-logging:\s*&logging[\s\S]*?max-size:\s*""?\d+m""?[\s\S]*?max-file:\s*""?\d+""?", compose);
        var bloqueApis = compose[compose.IndexOf("x-api: &api", StringComparison.Ordinal)..compose.IndexOf("services:", StringComparison.Ordinal)];
        Assert.Contains("logging: *logging", bloqueApis);

        foreach (var nombre in new[] { "postgres", "redis", "rabbitmq", "api1", "api2", "worker", "backoffice", "portal-comercio", "seguimiento-publico", "prometheus", "grafana", "caddy" })
        {
            var servicio = Servicio(compose, nombre);
            Assert.True(servicio.Contains("logging: *logging") || servicio.Contains("<<: *api"), $"El servicio '{nombre}' no rota sus registros.");
        }
    }

    [Fact]
    public void ElReceptorDePrueba_SoloSeHabilitaPorVariableYNuncaPorDefecto()
    {
        // La línea vive en el bloque de variables compartido por las dos APIs, no dentro de un servicio concreto
        Assert.Matches(@"Avisos__ReceptorDePrueba__Habilitado:\s*\$\{RECEPTOR_PRUEBA_AVISOS:-false\}", LeerCompose());
    }
}
