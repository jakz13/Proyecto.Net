using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace UltimaMilla.Integration.Tests;

/// <summary>El inicio de sesión se acota por origen: pasado el tope, la API responde 429 sin siquiera evaluar las credenciales.</summary>
[Collection(ColeccionApi.Nombre)]
public class LimiteDeLoginApiTests
{
    private readonly ApiFactory _factory;

    public LimiteDeLoginApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [FactPostgres]
    public async Task PasadoElTopePorMinuto_ElLoginResponde429()
    {
        using var fabrica = _factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimit:LoginPorMinuto"] = "3" })));
        var cliente = fabrica.CreateClient();

        var codigos = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            // Un correo que no existe: la respuesta es 401 hasta que el limitador corta antes de evaluar nada
            var respuesta = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = $"nadie.{i}@correo.com", password = "NoImporta.2026!" });
            codigos.Add(respuesta.StatusCode);
        }

        Assert.Equal(3, codigos.Count(c => c == HttpStatusCode.Unauthorized));
        Assert.Equal(3, codigos.Count(c => c == HttpStatusCode.TooManyRequests));
    }
}
