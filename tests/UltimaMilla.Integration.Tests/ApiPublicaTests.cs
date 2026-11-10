using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace UltimaMilla.Integration.Tests;

/// <summary>API pública para comercios (opcional 7.4): ambiente de pruebas y limitación de tasa diferenciada por clave.</summary>
[Collection(ColeccionApi.Nombre)]
public class ApiPublicaTests
{
    private readonly ApiFactory _factory;

    public ApiPublicaTests(ApiFactory factory) => _factory = factory;

    private static object Pedido(string? referencia = null) => new
    {
        modalidadServicio = "Estandar",
        nombreDestinatario = "Cliente de prueba",
        calle = "Av. Italia",
        numero = "100",
        ciudad = "Montevideo",
        bultos = new[] { new { peso = 2, alto = 10, ancho = 10, profundo = 10 } },
        referenciaExterna = referencia
    };

    private async Task<string> ClavePruebasDelComercioAsync()
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = "comercio@gmail.com", password = "Comercio.2026!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());

        var respuesta = await cliente.GetFromJsonAsync<JsonElement>("/api/comercio/clave-api-pruebas");
        return respuesta.GetProperty("clavePruebas").GetString()!;
    }

    [FactPostgres]
    public async Task LaClaveDePruebas_ValidaYCalculaLaTarifa_PeroNoRegistraNada()
    {
        var clave = await ClavePruebasDelComercioAsync();
        Assert.StartsWith("umt_", clave);

        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", clave);
        var referencia = "SBX-" + Guid.NewGuid().ToString("N")[..8];

        var respuesta = await cliente.PostAsJsonAsync("/api/integracion/v1/envios", Pedido(referencia));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(cuerpo.GetProperty("sandbox").GetBoolean());
        Assert.True(cuerpo.GetProperty("tarifa").GetDecimal() > 0);
        Assert.Equal(Guid.Empty, cuerpo.GetProperty("id").GetGuid());
        var codigo = cuerpo.GetProperty("codigoSeguimiento").GetString()!;
        Assert.StartsWith("SBX-", codigo);

        // No se creó nada: el código no existe para el seguimiento público
        var seguimiento = await _factory.CreateClient().GetAsync($"/api/seguimiento/{codigo}");
        Assert.Equal(HttpStatusCode.NotFound, seguimiento.StatusCode);

        // Repetir la misma referencia no devuelve "duplicado": en pruebas nunca se persiste
        var repetido = await cliente.PostAsJsonAsync("/api/integracion/v1/envios", Pedido(referencia));
        Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
        Assert.False((await repetido.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("duplicado").GetBoolean());
    }

    [FactPostgres]
    public async Task LaClaveDePruebas_ConLaFirmaAlterada_SeRechaza()
    {
        var clave = await ClavePruebasDelComercioAsync();
        var alterada = clave[..^3] + (clave.EndsWith("aaa") ? "bbb" : "aaa");

        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", alterada);
        var respuesta = await cliente.PostAsJsonAsync("/api/integracion/v1/envios", Pedido());

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [FactPostgres]
    public async Task ElLimiteDeTasaEsPorClave_YMasBajoParaLaClaveDePruebas()
    {
        // Claves distintas, cupos distintos: agotar una no afecta a otra
        var inventadaPruebas = "umt_" + Guid.NewGuid().ToString("N") + "_x";
        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", inventadaPruebas);

        var codigos = new List<HttpStatusCode>();
        for (var i = 0; i < 22; i++)
            codigos.Add((await cliente.PostAsJsonAsync("/api/integracion/v1/envios", Pedido())).StatusCode);

        Assert.Equal(20, codigos.Count(c => c == HttpStatusCode.Unauthorized));   // 20 intentos con clave inválida
        Assert.Equal(2, codigos.Count(c => c == HttpStatusCode.TooManyRequests)); // y el resto, frenado

        var otra = _factory.CreateClient();
        otra.DefaultRequestHeaders.Add("X-Api-Key", "otra-clave-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await otra.PostAsJsonAsync("/api/integracion/v1/envios", Pedido())).StatusCode);
    }

    [Fact]
    public async Task LaDocumentacionNavegableDeLaApiPublicaSoloIncluyeLasRutasDeIntegracion()
    {
        var doc = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/swagger/integracion/swagger.json");

        var rutas = doc.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains("/api/integracion/v1/envios", rutas);
        Assert.Contains("/api/comercio/clave-api-pruebas", rutas);
        Assert.All(rutas, r => Assert.True(r.StartsWith("/api/integracion/") || r == "/api/comercio/clave-api-pruebas", r));
        Assert.Equal("v1", doc.GetProperty("info").GetProperty("version").GetString());
    }
}
