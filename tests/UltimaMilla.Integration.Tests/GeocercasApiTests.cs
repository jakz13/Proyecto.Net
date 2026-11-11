using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Geocercas (llegada y salida de parada) de punta a punta con datos propios de cada prueba: la app informa los eventos,
/// el servidor los registra de forma idempotente y el comprador los ve en el seguimiento público.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class GeocercasApiTests
{
    private readonly ApiFactory _factory;

    public GeocercasApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];

    private async Task<HttpClient> ClienteComoAsync(string email, string password)
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        return cliente;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage respuesta, HttpStatusCode esperado)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == esperado, $"Se esperaba {(int)esperado} y llegó {(int)respuesta.StatusCode}: {cuerpo}");
        return string.IsNullOrWhiteSpace(cuerpo) ? default : JsonSerializer.Deserialize<JsonElement>(cuerpo);
    }

    [FactPostgres]
    public async Task Geocercas_LlegadaYSalida_SeRegistranUnaSolaVezYApareceEnElSeguimientoPublico()
    {
        var admin = await ClienteComoAsync("admin@gmail.com", "Admin.2026!");
        var despachador = await UsuariosDePrueba.DespachadorAsync(_factory);
        var comercio = await ClienteComoAsync("comercio@gmail.com", "Comercio.2026!");
        var deposito = await ClienteComoAsync("deposito@gmail.com", "Deposito.2026!");

        // Repartidor y vehículo propios: los tests comparten la base
        var sufijo = Sufijo();
        var emailRepartidor = $"geocercas.{sufijo}@ultimamilla.com.uy";
        var repartidor = await Json(await admin.PostAsJsonAsync("/api/v1/flota/repartidores", new
        {
            nombreCompleto = "Repartidor " + sufijo, email = emailRepartidor, documento = "4.111.222-3", telefono = "099000222", password = "Repartidor.2026!"
        }), HttpStatusCode.Created);
        var vehiculo = await Json(await admin.PostAsJsonAsync("/api/v1/flota/vehiculos", new
        {
            patente = "G" + sufijo.ToUpperInvariant(), tipo = "Furgón", capacidadPeso = 500, capacidadVolumen = 5
        }), HttpStatusCode.Created);
        var app = await ClienteComoAsync(emailRepartidor, "Repartidor.2026!");

        // Un envío recibido y clasificado en depósito
        var alta = await Json(await comercio.PostAsJsonAsync("/api/envios", new
        {
            modalidadServicio = "Estandar",
            nombreDestinatario = "Cliente " + sufijo,
            calle = "Av. Rivera",
            numero = "2500",
            ciudad = "Montevideo",
            bultos = new[] { new { peso = 2, alto = 10, ancho = 10, profundo = 10 } }
        }), HttpStatusCode.Created);
        var envioId = alta.GetProperty("id").GetGuid();
        var codigo = alta.GetProperty("codigoSeguimiento").GetString()!;
        var bulto = alta.GetProperty("bultos")[0].GetString()!;
        await Json(await deposito.PostAsJsonAsync($"/api/deposito/envios/{envioId}/recepcion", new { codigosBultosEscaneados = new[] { bulto } }), HttpStatusCode.OK);
        await Json(await deposito.PostAsJsonAsync($"/api/deposito/envios/{envioId}/clasificacion",
            new { bultos = new[] { new { codigoBulto = bulto, prioridad = "Normal" } } }), HttpStatusCode.OK);

        // Hoja de ruta, carga informada por la app y despacho por el depósito
        var ruta = await Json(await despachador.PostAsJsonAsync("/api/planificacion/hojas-de-ruta", new
        {
            repartidorId = repartidor.GetProperty("id").GetGuid(),
            vehiculoId = vehiculo.GetProperty("id").GetGuid(),
            fecha = DateOnly.FromDateTime(DateTime.UtcNow),
            enviosIds = new[] { envioId }
        }), HttpStatusCode.Created);
        var rutaId = ruta.GetProperty("hojaDeRutaId").GetGuid();
        var paradaId = ruta.GetProperty("paradas")[0].GetProperty("paradaId").GetGuid();

        await Json(await app.PostAsJsonAsync($"/api/mobile/v1/rutas/{rutaId}/carga/confirmar",
            new { codigosBultosCargados = new[] { bulto }, idempotencyKey = Guid.NewGuid().ToString() }), HttpStatusCode.OK);
        await Json(await despachador.PostAsJsonAsync($"/api/planificacion/hojas-de-ruta/{rutaId}/despacho", new { }), HttpStatusCode.OK);

        // LLEGADA_GEOCERCA a menos de 100 m
        var llegada = new
        {
            envioId,
            tipoEvento = "LLEGADA_GEOCERCA",
            latitud = -34.905395,
            longitud = -56.1885,
            distanciaMetros = 45.0,
            idempotencyKey = "geo-in-" + Guid.NewGuid()
        };
        var respuestaLlegada = await Json(await app.PostAsJsonAsync($"/api/mobile/v1/rutas/{rutaId}/paradas/{paradaId}/geocerca", llegada), HttpStatusCode.OK);
        Assert.Equal("LlegadaGeocerca", respuestaLlegada.GetProperty("tipoEvento").GetString());
        Assert.False(respuestaLlegada.GetProperty("idempotente").GetBoolean());

        // Reintento con la misma clave: no duplica el evento
        var reintento = await Json(await app.PostAsJsonAsync($"/api/mobile/v1/rutas/{rutaId}/paradas/{paradaId}/geocerca", llegada), HttpStatusCode.OK);
        Assert.True(reintento.GetProperty("idempotente").GetBoolean());

        // SALIDA_GEOCERCA a más de 150 m, con el tiempo de permanencia
        var salida = new
        {
            envioId,
            tipoEvento = "SALIDA_GEOCERCA",
            latitud = -34.903820,
            longitud = -56.1885,
            distanciaMetros = 220.0,
            tiempoPermanenciaSegundos = 95,
            idempotencyKey = "geo-out-" + Guid.NewGuid()
        };
        var respuestaSalida = await Json(await app.PostAsJsonAsync($"/api/mobile/v1/rutas/{rutaId}/paradas/{paradaId}/geocerca", salida), HttpStatusCode.OK);
        Assert.Equal("SalidaGeocerca", respuestaSalida.GetProperty("tipoEvento").GetString());

        // El comprador ve ambos hitos en el seguimiento público (la caché se invalidó al registrarlos)
        var publico = _factory.CreateClient();
        var seguimiento = await Json(await publico.GetAsync($"/api/seguimiento/{codigo}"), HttpStatusCode.OK);
        var hitos = seguimiento.GetProperty("historialEventos").EnumerateArray()
            .Select(h => h.GetProperty("descripcionPublica").GetString() ?? string.Empty)
            .ToList();

        Assert.Contains(hitos, d => d.Contains("Repartidor ingresó a la zona de entrega", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hitos, d => d.Contains("Repartidor salió de la zona de entrega (Permanencia: 95s)", StringComparison.OrdinalIgnoreCase));
        Assert.Single(hitos, d => d.Contains("ingresó a la zona de entrega", StringComparison.OrdinalIgnoreCase));
    }
}
