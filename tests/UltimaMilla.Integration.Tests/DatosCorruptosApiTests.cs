using System.Net;
using System.Net.Http.Json;
using UltimaMilla.Frontend.Contracts.Modelos.Comercios;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;
using UltimaMilla.Frontend.Infrastructure.Http;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Un cliente que llama a la API sin pasar por las pantallas (por ejemplo, desde la consola del navegador) no puede guardar datos
/// corruptos. Reproduce la prueba del 16/11 contra el servidor, donde la API aceptó un comercio con email "no-es-email" y teléfono
/// "abc", zonas con polígonos basura y un color "javascript:alert(1)". Cada caso devolvía 201 antes de la validación de dominio.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class DatosCorruptosApiTests
{
    private readonly ApiFactory _factory;

    public DatosCorruptosApiTests(ApiFactory factory) => _factory = factory;

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];
    private static string Rut() => string.Concat(Enumerable.Range(0, 12).Select(_ => Random.Shared.Next(0, 10)));

    private static CrearComercioDto ComercioValido() => new()
    {
        RazonSocial = "Comercio Prueba " + Sufijo(), NombreFantasia = "Prueba", Rut = Rut(),
        Email = $"prueba.{Sufijo()}@empresa.com", Telefono = "099123456", Direccion = "Av. Italia 3456"
    };

    // ───────────────────────────── Comercios ─────────────────────────────

    [Theory]
    [InlineData("no-es-email", "099123456")]
    [InlineData("a@@b", "099123456")]
    [InlineData("a@b", "099123456")]
    [InlineData("valido@empresa.com", "abc")]
    [InlineData("valido@empresa.com", "29001122")]       // 8 dígitos
    [InlineData("valido@empresa.com", "0991234567")]     // 10 dígitos
    [InlineData("valido@empresa.com", "'; DROP TABLE \"Envios\";--")]
    public async Task Comercio_ConContactoInvalido_ApiResponde400(string email, string telefono)
    {
        var api = new HttpComerciosApi(_factory.CreateClient());
        var dto = ComercioValido() with { Email = email, Telefono = telefono };

        var error = await Assert.ThrowsAsync<ApiExcepcion>(() => api.CrearComercioAsync(dto));
        Assert.Equal(HttpStatusCode.BadRequest, error.CodigoEstado);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("1234")]
    public async Task Comercio_ConDireccionInvalida_ApiResponde400(string direccion)
    {
        var api = new HttpComerciosApi(_factory.CreateClient());
        var error = await Assert.ThrowsAsync<ApiExcepcion>(() => api.CrearComercioAsync(ComercioValido() with { Direccion = direccion }));
        Assert.Equal(HttpStatusCode.BadRequest, error.CodigoEstado);
    }

    [Fact]
    public async Task Comercio_ConMarcadoEnLaRazonSocial_ApiResponde400()
    {
        var api = new HttpComerciosApi(_factory.CreateClient());
        var error = await Assert.ThrowsAsync<ApiExcepcion>(() => api.CrearComercioAsync(ComercioValido() with { RazonSocial = "ZZ <b>x</b>" }));
        Assert.Equal(HttpStatusCode.BadRequest, error.CodigoEstado);
    }

    [Fact]
    public async Task Comercio_Valido_SeGuardaConTelefonoDeNueveDigitos()
    {
        var api = new HttpComerciosApi(_factory.CreateClient());
        var creado = await api.CrearComercioAsync(ComercioValido() with { Telefono = "099 123 456" });
        Assert.Equal("099123456", creado.Telefono);
    }

    [Fact]
    public async Task Comercio_EditarConDatosInvalidos_NoCambiaNada()
    {
        var api = new HttpComerciosApi(_factory.CreateClient());
        var creado = await api.CrearComercioAsync(ComercioValido());

        var error = await Assert.ThrowsAsync<ApiExcepcion>(() => api.ActualizarComercioAsync(creado.Id, new ActualizarComercioDto
        {
            RazonSocial = creado.RazonSocial, NombreFantasia = creado.NombreFantasia, Email = "a@@b", Telefono = "123",
            Direccion = creado.Direccion, Activo = true, SelloModificacion = creado.SelloModificacion
        }));
        Assert.Equal(HttpStatusCode.BadRequest, error.CodigoEstado);

        var releido = await api.ObtenerPorIdAsync(creado.Id);
        Assert.Equal(creado.Email, releido!.Email);
        Assert.Equal(creado.Telefono, releido.Telefono);
    }

    // ───────────────────────────── Zonas ─────────────────────────────

    [Theory]
    [InlineData("esto no es un polígono")]
    [InlineData("[[9999,9999],[9999,9998],[9998,9998],[9999,9999]]")]
    [InlineData("[[-34.8,-56.0],[-34.9,-56.1]]")]
    [InlineData("[[NaN,NaN],[1,1],[2,2],[NaN,NaN]]")]
    [InlineData("[[0,0]]")]
    public async Task Zona_ConPoligonoInvalido_ApiResponde400(string poligono)
    {
        var api = new HttpZonasCoberturaApi(_factory.CreateClient());
        var error = await Assert.ThrowsAsync<ApiExcepcion>(() => api.CrearZonaAsync(new CrearZonaCoberturaDto
        {
            Nombre = "Zona rara " + Sufijo(), PoligonoGeografico = poligono, ColorHex = "#000000"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, error.CodigoEstado);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("rojo")]
    [InlineData("#12")]
    public async Task Zona_ConColorInvalido_ApiResponde400(string color)
    {
        var api = new HttpZonasCoberturaApi(_factory.CreateClient());
        var error = await Assert.ThrowsAsync<ApiExcepcion>(() => api.CrearZonaAsync(new CrearZonaCoberturaDto
        {
            Nombre = "Zona color " + Sufijo(), PoligonoGeografico = "[[-34.8,-56.0],[-34.9,-56.1],[-34.9,-56.0]]", ColorHex = color
        }));
        Assert.Equal(HttpStatusCode.BadRequest, error.CodigoEstado);
    }

    [Fact]
    public async Task Zona_EditarConPoligonoBasura_NoCambiaElPoligono()
    {
        var api = new HttpZonasCoberturaApi(_factory.CreateClient());
        const string valido = "[[-34.8,-56.0],[-34.9,-56.1],[-34.9,-56.0]]";
        var creada = await api.CrearZonaAsync(new CrearZonaCoberturaDto { Nombre = "Zona ok " + Sufijo(), PoligonoGeografico = valido, ColorHex = "#16a34a" });

        var error = await Assert.ThrowsAsync<ApiExcepcion>(() => api.ActualizarZonaAsync(creada.Id, new ActualizarZonaCoberturaDto
        {
            Nombre = creada.Nombre, PoligonoGeografico = "basura", ColorHex = "#16a34a", Activa = true
        }));
        Assert.Equal(HttpStatusCode.BadRequest, error.CodigoEstado);
        Assert.Equal(valido, (await api.ObtenerPorIdAsync(creada.Id))!.PoligonoGeografico);
    }

    // ───────────────────────────── Flota ─────────────────────────────

    [Theory]
    [InlineData("x", "099123456")]
    [InlineData("repartidor@empresa.com", "12345")]
    [InlineData("repartidor@empresa.com", "abc")]
    public async Task Repartidor_ConContactoInvalido_ApiResponde400(string email, string telefono)
    {
        var api = new HttpFlotaApi(_factory.CreateClient());
        var error = await Assert.ThrowsAsync<ApiExcepcion>(() => api.CrearRepartidorAsync(new CrearRepartidorDto
        {
            NombreCompleto = "ZZ Prueba " + Sufijo(), Email = email, Documento = "12345678", Telefono = telefono
        }));
        Assert.Equal(HttpStatusCode.BadRequest, error.CodigoEstado);
    }

    // ───────────────────────────── Cuerpos mal formados ─────────────────────────────

    [Theory]
    [InlineData("/api/comercios", "{\"razonSocial\": ")]            // JSON cortado
    [InlineData("/api/comercios", "[1,2,3]")]                       // arreglo en vez de objeto
    [InlineData("/api/zonas-cobertura", "{\"nombre\":\"x\",\"franjasHorarias\":[null]}")]
    public async Task CuerpoMalFormado_NuncaEs500(string ruta, string cuerpo)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.PostAsync(ruta, new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json"));
        Assert.True((int)respuesta.StatusCode is >= 400 and < 500, $"Se esperaba un error 4xx y llegó {(int)respuesta.StatusCode}");
    }
}
