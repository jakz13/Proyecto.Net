using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Una hoja de ruta solo la opera el repartidor al que está asignada. Antes bastaba con ser repartidor del mismo operador para iniciar,
/// entregar o rendir la ruta de otro y leer los destinatarios y direcciones de sus paradas.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class RutaDeOtroRepartidorApiTests
{
    private readonly ApiFactory _factory;

    public RutaDeOtroRepartidorApiTests(ApiFactory factory)
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

    private async Task<(Guid RepartidorId, Guid VehiculoId, HttpClient Sesion)> NuevaDotacionAsync(HttpClient admin)
    {
        var sufijo = Sufijo();
        var email = $"ajena.{sufijo}@ultimamilla.com.uy";
        var repartidor = await admin.PostAsJsonAsync("/api/v1/flota/repartidores", new
        {
            nombreCompleto = "Repartidor " + sufijo, email, documento = "4.111.222-3", telefono = "099000222", password = "Repartidor.2026!"
        });
        Assert.Equal(HttpStatusCode.Created, repartidor.StatusCode);
        var vehiculo = await admin.PostAsJsonAsync("/api/v1/flota/vehiculos", new { patente = "A" + sufijo.ToUpperInvariant(), tipo = "Furgón", capacidadPeso = 500, capacidadVolumen = 5 });
        Assert.Equal(HttpStatusCode.Created, vehiculo.StatusCode);

        return ((await repartidor.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(),
                (await vehiculo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(),
                await ClienteComoAsync(email, "Repartidor.2026!"));
    }

    [FactPostgres]
    public async Task UnRepartidorNoPuedeOperarNiVerLaHojaDeRutaDeOtro()
    {
        var admin = await ClienteComoAsync("admin@gmail.com", "Admin.2026!");
        var comercio = await ClienteComoAsync("comercio@gmail.com", "Comercio.2026!");
        var deposito = await ClienteComoAsync("deposito@gmail.com", "Deposito.2026!");
        var despachador = await UsuariosDePrueba.DespachadorAsync(_factory);

        var duenio = await NuevaDotacionAsync(admin);
        var intruso = await NuevaDotacionAsync(admin);

        var alta = await comercio.PostAsJsonAsync("/api/envios", new
        {
            modalidadServicio = "Estandar", nombreDestinatario = "Cliente " + Sufijo(), calle = "Av. Rivera", numero = "2500", ciudad = "Montevideo",
            bultos = new[] { new { peso = 2, alto = 10, ancho = 10, profundo = 10 } }
        });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var envio = await alta.Content.ReadFromJsonAsync<JsonElement>();
        var envioId = envio.GetProperty("id").GetGuid();
        var bulto = envio.GetProperty("bultos")[0].GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await deposito.PostAsJsonAsync($"/api/deposito/envios/{envioId}/recepcion", new { codigosBultosEscaneados = new[] { bulto } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await deposito.PostAsJsonAsync($"/api/deposito/envios/{envioId}/clasificacion",
            new { bultos = new[] { new { codigoBulto = bulto, prioridad = "Normal" } } })).StatusCode);

        var armada = await despachador.PostAsJsonAsync("/api/planificacion/hojas-de-ruta", new
        {
            repartidorId = duenio.RepartidorId, vehiculoId = duenio.VehiculoId, fecha = DateOnly.FromDateTime(DateTime.UtcNow),
            enviosIds = new[] { envioId }, retirosIds = Array.Empty<Guid>()
        });
        Assert.Equal(HttpStatusCode.Created, armada.StatusCode);
        var rutaId = (await armada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("hojaDeRutaId").GetGuid();
        var baseRuta = $"/api/mobile/v1/rutas/{rutaId}";

        // El intruso (otro repartidor del mismo operador) no puede ver ni operar la ruta
        Assert.Equal(HttpStatusCode.Forbidden, (await intruso.Sesion.GetAsync($"{baseRuta}/paradas")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await intruso.Sesion.PostAsJsonAsync($"{baseRuta}/carga/confirmar",
            new { codigosBultosCargados = new[] { bulto }, idempotencyKey = Guid.NewGuid().ToString() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await intruso.Sesion.PostAsJsonAsync($"{baseRuta}/iniciar-recorrido", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await intruso.Sesion.PostAsJsonAsync($"{baseRuta}/finalizar", new { codigosBultosDevueltos = Array.Empty<string>() })).StatusCode);

        // El dueño sí puede
        Assert.Equal(HttpStatusCode.OK, (await duenio.Sesion.PostAsJsonAsync($"{baseRuta}/carga/confirmar",
            new { codigosBultosCargados = new[] { bulto }, idempotencyKey = Guid.NewGuid().ToString() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await duenio.Sesion.GetAsync($"{baseRuta}/paradas")).StatusCode);
    }
}
