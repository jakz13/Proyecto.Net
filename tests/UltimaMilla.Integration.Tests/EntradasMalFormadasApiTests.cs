using System.Net;
using System.Text;
using System.Text.Json;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Datos mal formados que antes provocaban un error 500 o dejaban basura guardada (auditoría del 14/11): deben dar 400 con un mensaje claro
/// y no cambiar nada. Se prueba con el cuerpo JSON crudo, como lo mandaría alguien desde la consola del navegador.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class EntradasMalFormadasApiTests
{
    private readonly ApiFactory _factory;

    public EntradasMalFormadasApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<int> VersionVigenteAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetAsync("/api/cuadros-tarifarios/vigente");
        if (respuesta.StatusCode != HttpStatusCode.OK) return -1;
        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return documento.RootElement.GetProperty("version").GetInt32();
    }

    [FactPostgres]
    public async Task Zona_ConUnaFranjaNula_Da400EnVezDeError500()
    {
        var respuesta = await _factory.CreateClient().PostAsync("/api/zonas-cobertura",
            Json("{\"nombre\":\"IT zona con franja nula\",\"franjasHorarias\":[null]}"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [FactPostgres]
    public async Task ValidarFranjas_ConUnaFranjaNula_Da400EnVezDeError500()
    {
        var respuesta = await _factory.CreateClient().PostAsync("/api/zonas-cobertura/validar-franjas", Json("[null]"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [FactPostgres]
    public async Task CuadroTarifario_ConReglasNulasOVacias_Da400YNoCambiaLaVersionVigente()
    {
        // Antes, {"reglasTarifa":[{}]} publicaba una versión con una regla armada solo con valores por defecto y dejaba al operador sin su tarifa real
        var cliente = _factory.CreateClient();
        var antes = await VersionVigenteAsync(cliente);

        foreach (var cuerpo in new[] { "{\"reglasTarifa\":[null]}", "{\"reglasTarifa\":[{}]}" })
        {
            var respuesta = await cliente.PostAsync("/api/cuadros-tarifarios", Json(cuerpo));
            Assert.True(respuesta.StatusCode == HttpStatusCode.BadRequest, $"{cuerpo} -> {(int)respuesta.StatusCode}");
        }

        Assert.Equal(antes, await VersionVigenteAsync(cliente));
    }

    [FactPostgres]
    public async Task UnCaracterNuloEnUnParametroDeConsulta_Da400EnVezDeServicioNoDisponible()
    {
        // La base rechaza el carácter nulo en un texto (SQLSTATE 22021): el manejador global lo mostraba como "servicio no disponible" (503) (D-19)
        var cliente = _factory.CreateClient();

        foreach (var ruta in new[] { "/api/v1/flota/repartidores", "/api/v1/mensajeria/cola-fallidos/" })
        {
            var respuesta = await cliente.GetAsync(ruta + "?texto=%00&q=%00&busqueda=%00&estado=%00&codigo=%00&tipo=%00");
            Assert.True(respuesta.StatusCode == HttpStatusCode.BadRequest, $"{ruta} -> {(int)respuesta.StatusCode}");
        }
    }

    [FactPostgres]
    public async Task Posiciones_ConUnaPosicionNulaOSinCoordenadas_Da400()
    {
        // {"posiciones":[null]} daba 500 y {"posiciones":[{}]} guardaba una posición en (0, 0)
        var cliente = _factory.CreateClient();

        foreach (var cuerpo in new[] { "{\"posiciones\":[null]}", "{\"posiciones\":[{}]}" })
        {
            var respuesta = await cliente.PostAsync("/api/mobile/v1/posiciones", Json(cuerpo));
            Assert.True(respuesta.StatusCode == HttpStatusCode.BadRequest, $"{cuerpo} -> {(int)respuesta.StatusCode}");
        }
    }
}
