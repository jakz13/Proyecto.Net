using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

[Collection(ColeccionApi.Nombre)]
public class ComercioDadoDeBajaApiTests
{
    private readonly ApiFactory _factory;

    public ComercioDadoDeBajaApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> ClienteComoAsync(string email, string password)
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.True(login.StatusCode == HttpStatusCode.OK, $"Login {email}: {(int)login.StatusCode} {await login.Content.ReadAsStringAsync()}");
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        return cliente;
    }

    private HttpClient ClienteConToken(string token)
    {
        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    [FactPostgres]
    public async Task BajaDeComercio_SoloLecturaHistoricaYCancelaOperacionesPreviasASalida()
    {
        var operador1 = DatabaseSeeder.DefaultOperadorId.ToString();
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var rut = string.Concat(Enumerable.Range(0, 12).Select(i => (Math.Abs(sufijo.GetHashCode()) + i * 7) % 10));
        var email = $"baja.{sufijo}@ultimamilla.com.uy";

        var admin1 = await ClienteComoAsync("admin@gmail.com", "Admin.2026!");
        var alta = await admin1.PostAsJsonAsync("/api/comercios", new
        {
            razonSocial = "Tienda Baja " + sufijo,
            nombreFantasia = "Baja",
            rut,
            email,
            telefono = "099222333",
            direccion = "Av. Rivera 100"
        });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var comercioId = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var usuario = await admin1.PostAsJsonAsync("/api/usuarios", new
        {
            nombre = "Usuario",
            apellido = "Baja",
            email,
            password = "Baja.2026!",
            perfilId = DatabaseSeeder.DefaultPerfilComercioId,
            comercioId
        });
        Assert.Equal(HttpStatusCode.Created, usuario.StatusCode);

        var comercio = await ClienteComoAsync(email, "Baja.2026!");

        // Crear un envío admitido y un retiro solicitado mientras está activo
        var crearEnvio = await comercio.PostAsJsonAsync("/api/envios", new
        {
            modalidadServicio = "Estandar",
            nombreDestinatario = "Cliente Histórico",
            calle = "Av. Brasil",
            numero = "200",
            ciudad = "Montevideo",
            bultos = new[] { new { peso = 1.5, alto = 10, ancho = 10, profundo = 10 } }
        });
        Assert.Equal(HttpStatusCode.Created, crearEnvio.StatusCode);
        var envioId = (await crearEnvio.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var crearRetiro = await comercio.PostAsJsonAsync("/api/comercio/retiros", new
        {
            nombreContacto = "Contacto Retiro",
            calle = "Av. Brasil",
            numero = "200",
            ciudad = "Montevideo",
            motivo = "Cambio",
            bultos = new[] { new { descripcion = "Caja", peso = 1.0, alto = 10, ancho = 10, profundo = 10 } }
        });
        Assert.Equal(HttpStatusCode.Created, crearRetiro.StatusCode);
        var retiroId = (await crearRetiro.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // El operador 1 da de baja al comercio
        var baja = await admin1.PatchAsJsonAsync($"/api/comercios/{comercioId}/estado", new { activo = false });
        Assert.True(baja.StatusCode == HttpStatusCode.NoContent || baja.StatusCode == HttpStatusCode.OK, await baja.Content.ReadAsStringAsync());

        // El comercio sigue viendo al operador como dado de baja y puede iniciar sesión para consultar el pasado
        var comercioReingreso = await ClienteComoAsync(email, "Baja.2026!");
        var operadoresJson = await comercioReingreso.GetFromJsonAsync<JsonElement>("/api/comercio/operadores");
        Assert.True(operadoresJson.GetProperty("operadorActualDadoDeBaja").GetBoolean());
        var opItem = Assert.Single(operadoresJson.GetProperty("operadores").EnumerateArray());
        Assert.Equal(operador1, opItem.GetProperty("operadorId").GetString());
        Assert.False(opItem.GetProperty("activo").GetBoolean());

        // Cambios/consultas de lectura permitidos (200 OK) y el envío + retiro quedaron Cancelado
        var detalleEnvio = await comercioReingreso.GetFromJsonAsync<JsonElement>($"/api/envios/{envioId}/detalle");
        Assert.Equal("Cancelado", detalleEnvio.GetProperty("estado").GetString());

        var detalleRetiro = await comercioReingreso.GetFromJsonAsync<JsonElement>($"/api/comercio/retiros/{retiroId}");
        Assert.Equal("Cancelado", detalleRetiro.GetProperty("estado").GetString());

        Assert.Equal(HttpStatusCode.OK, (await comercioReingreso.GetAsync("/api/comercio/envios")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await comercioReingreso.GetAsync("/api/comercio/retiros")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await comercioReingreso.GetAsync("/api/comercio/devoluciones")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await comercioReingreso.GetAsync("/api/comercio/avisos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await comercioReingreso.GetAsync("/api/comercio/avisos/historial")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await comercioReingreso.GetAsync("/api/comercio/webhooks/suscripciones")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await comercioReingreso.GetAsync("/api/comercio/cuenta-corriente")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await comercioReingreso.GetAsync("/api/comercio/liquidaciones")).StatusCode);

        // Todas las mutaciones quedan bloqueadas con 403 Forbidden
        var nuevoEnvio = await comercioReingreso.PostAsJsonAsync("/api/envios", new
        {
            modalidadServicio = "Estandar",
            nombreDestinatario = "Bloqueado",
            calle = "Av. Brasil",
            numero = "201",
            ciudad = "Montevideo",
            bultos = new[] { new { peso = 1, alto = 10, ancho = 10, profundo = 10 } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, nuevoEnvio.StatusCode);

        var etiquetas = await comercioReingreso.PostAsJsonAsync("/api/comercio/envios/etiquetas", new { envioIds = new[] { envioId } });
        Assert.Equal(HttpStatusCode.Forbidden, etiquetas.StatusCode);

        var nuevoRetiro = await comercioReingreso.PostAsJsonAsync("/api/comercio/retiros", new
        {
            nombreContacto = "Bloqueado",
            calle = "Av. Brasil",
            numero = "201",
            ciudad = "Montevideo",
            motivo = "Prueba",
            bultos = new[] { new { descripcion = "Caja", peso = 1, alto = 10, ancho = 10, profundo = 10 } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, nuevoRetiro.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await comercioReingreso.PostAsync("/api/comercio/clave-api", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await comercioReingreso.PostAsJsonAsync("/api/comercio/avisos/probar", new { urlWebhook = "https://ejemplo.uy/hook" })).StatusCode);

        // Puede abrir sesión explícitamente con el operador dado de baja para consultar (no devuelve 404)
        var sesionBaja = await comercioReingreso.PostAsync($"/api/comercio/operadores/{operador1}/sesion", null);
        Assert.Equal(HttpStatusCode.OK, sesionBaja.StatusCode);
        var tokenBaja = (await sesionBaja.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        var clienteBaja = ClienteConToken(tokenBaja);
        Assert.Equal(HttpStatusCode.OK, (await clienteBaja.GetAsync("/api/comercio/envios")).StatusCode);
    }
}

