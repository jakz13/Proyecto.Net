using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Un comercio con el mismo RUT puede estar vinculado a varios operadores. Se prueba el flujo completo con sesiones reales:
/// el segundo operador lo da de alta con el mismo RUT, el comercio ve a ambos y puede abrir sesión con cualquiera de ellos,
/// y cada sesión sigue acotada a un único operador.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class ComercioConVariosOperadoresApiTests
{
    private readonly ApiFactory _factory;

    public ComercioConVariosOperadoresApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> ClienteComoAsync(string email, string password)
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.True(login.StatusCode == HttpStatusCode.OK, $"Inicio de sesión de {email}: {(int)login.StatusCode} {await login.Content.ReadAsStringAsync()}");
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

    private async Task SembrarSegundoOperadorAsync()
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseSeederSegundoOperador.SembrarAsync(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
    }

    [FactPostgres]
    public async Task MismoRutEnDosOperadores_ElComercioVeAmbosYPuedeOperarConCadaUno()
    {
        await SembrarSegundoOperadorAsync();

        var operador1 = DatabaseSeeder.DefaultOperadorId.ToString();
        var operador2 = DatabaseSeederSegundoOperador.OperadorId.ToString();

        // El operador 1 da de alta el comercio y su usuario
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var rut = string.Concat(Enumerable.Range(0, 12).Select(_ => Random.Shared.Next(0, 10)));
        var email = $"multi.{sufijo}@ultimamilla.com.uy";
        var admin1 = await ClienteComoAsync("admin@gmail.com", "Admin.2026!");
        var alta1 = await admin1.PostAsJsonAsync("/api/comercios", new
        {
            razonSocial = "Tienda Multi " + sufijo, nombreFantasia = "Multi", rut, email, telefono = "099111222", direccion = "Av. Italia 1"
        });
        Assert.Equal(HttpStatusCode.Created, alta1.StatusCode);
        var comercioId = (await alta1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var usuario = await admin1.PostAsJsonAsync("/api/usuarios", new
        {
            nombre = "Usuario", apellido = "Multi", email, password = "Multi.2026!",
            perfilId = DatabaseSeeder.DefaultPerfilComercioId, comercioId
        });
        Assert.True(usuario.StatusCode == HttpStatusCode.Created, await usuario.Content.ReadAsStringAsync());

        // Con un solo operador, el comercio ve solo a ese
        var comercio = await ClienteComoAsync(email, "Multi.2026!");
        var antes = await comercio.GetFromJsonAsync<JsonElement>("/api/comercio/operadores");
        Assert.Equal(operador1, antes.GetProperty("operadorActualId").GetString());
        Assert.Single(antes.GetProperty("operadores").EnumerateArray());

        // El mismo operador no puede darlo de alta dos veces
        var repetido = await admin1.PostAsJsonAsync("/api/comercios", new
        {
            razonSocial = "Tienda Multi " + sufijo, nombreFantasia = "Multi", rut, email = "otro." + email, telefono = "099111222", direccion = "Av. Italia 1"
        });
        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);

        // El operador 2 lo da de alta con el mismo RUT: es el mismo comercio, con un vínculo nuevo
        var admin2 = await ClienteComoAsync("admin.express@ultimamilla.com.uy", "demo1234");
        var alta2 = await admin2.PostAsJsonAsync("/api/comercios", new
        {
            razonSocial = "Tienda Multi " + sufijo, nombreFantasia = "Multi", rut, email, telefono = "099111222", direccion = "Av. Italia 1"
        });
        Assert.Equal(HttpStatusCode.Created, alta2.StatusCode);
        Assert.Equal(comercioId, (await alta2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());

        // Ahora el comercio ve a ambos, sin volver a iniciar sesión
        var despues = await comercio.GetFromJsonAsync<JsonElement>("/api/comercio/operadores");
        var ids = despues.GetProperty("operadores").EnumerateArray().Select(o => o.GetProperty("operadorId").GetString()).ToList();
        Assert.Contains(operador1, ids);
        Assert.Contains(operador2, ids);
        Assert.Equal(operador1, despues.GetProperty("operadorActualId").GetString());

        // Abre sesión con el operador 2: el token nuevo opera solo en ese operador
        var cambio = await comercio.PostAsync($"/api/comercio/operadores/{operador2}/sesion", null);
        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);
        var sesion = await cambio.Content.ReadFromJsonAsync<JsonElement>();
        var conOperador2 = ClienteConToken(sesion.GetProperty("token").GetString()!);

        var comoOperador2 = await conOperador2.GetFromJsonAsync<JsonElement>("/api/comercio/operadores");
        Assert.Equal(operador2, comoOperador2.GetProperty("operadorActualId").GetString());

        var envio = await conOperador2.PostAsJsonAsync("/api/envios", new
        {
            modalidadServicio = "Estandar", nombreDestinatario = "Cliente Multi", calle = "Av. Italia", numero = "9", ciudad = "Montevideo",
            bultos = new[] { new { peso = 1, alto = 10, ancho = 10, profundo = 10 } }
        });
        Assert.True(envio.StatusCode == HttpStatusCode.Created, await envio.Content.ReadAsStringAsync());
        var envioId = (await envio.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // El envío pertenece al operador 2: la sesión del mismo comercio con el operador 1 no lo ve
        Assert.Equal(HttpStatusCode.OK, (await conOperador2.GetAsync($"/api/envios/{envioId}/detalle")).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await comercio.GetAsync($"/api/envios/{envioId}/detalle")).StatusCode);

        // Y se puede volver al operador 1 desde la sesión del operador 2
        var vuelta = await conOperador2.PostAsync($"/api/comercio/operadores/{operador1}/sesion", null);
        Assert.Equal(HttpStatusCode.OK, vuelta.StatusCode);
    }

    [FactPostgres]
    public async Task ComercioDadoDeBaja_ConUnTokenAbiertoYaNoPuedeOperar()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var rut = string.Concat(Enumerable.Range(0, 12).Select(_ => Random.Shared.Next(0, 10)));
        var email = $"baja.{sufijo}@ultimamilla.com.uy";
        var admin = await ClienteComoAsync("admin@gmail.com", "Admin.2026!");
        var alta = await admin.PostAsJsonAsync("/api/comercios", new
        {
            razonSocial = "Tienda Baja " + sufijo, nombreFantasia = "Baja", rut, email, telefono = "099111222", direccion = "Av. Italia 2"
        });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var comercioId = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var usuario = await admin.PostAsJsonAsync("/api/usuarios", new
        {
            nombre = "Usuario", apellido = "Baja", email, password = "Baja.2026!",
            perfilId = DatabaseSeeder.DefaultPerfilComercioId, comercioId
        });
        Assert.True(usuario.StatusCode == HttpStatusCode.Created, await usuario.Content.ReadAsStringAsync());

        var comercio = await ClienteComoAsync(email, "Baja.2026!");
        Assert.Equal(HttpStatusCode.OK, (await comercio.GetAsync("/api/comercio/operadores")).StatusCode);

        var baja = await admin.PatchAsJsonAsync($"/api/comercios/{comercioId}/estado", new { activo = false });
        Assert.True(baja.IsSuccessStatusCode, await baja.Content.ReadAsStringAsync());

        // El token seguía vigente, pero la baja corta cualquier acción del comercio
        Assert.Equal(HttpStatusCode.Forbidden, (await comercio.GetAsync("/api/comercio/operadores")).StatusCode);
        var envio = await comercio.PostAsJsonAsync("/api/envios", new
        {
            modalidadServicio = "Estandar", nombreDestinatario = "Cliente", calle = "Av. Italia", numero = "9", ciudad = "Montevideo",
            bultos = new[] { new { peso = 1, alto = 10, ancho = 10, profundo = 10 } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, envio.StatusCode);
    }

    [FactPostgres]
    public async Task NoSePuedeAbrirSesionConUnOperadorSinVinculoNiDesdeUnPerfilQueNoEsDeComercio()
    {
        await SembrarSegundoOperadorAsync();

        var comercio = await ClienteComoAsync("comercio@gmail.com", "Comercio.2026!");
        Assert.Equal(HttpStatusCode.NotFound, (await comercio.PostAsync($"/api/comercio/operadores/{Guid.NewGuid()}/sesion", null)).StatusCode);

        // El operador 2 existe pero este comercio no tiene vínculo con él
        var lista = await comercio.GetFromJsonAsync<JsonElement>("/api/comercio/operadores");
        Assert.DoesNotContain(lista.GetProperty("operadores").EnumerateArray(),
            o => o.GetProperty("operadorId").GetString() == DatabaseSeederSegundoOperador.OperadorId.ToString());
        Assert.Equal(HttpStatusCode.NotFound,
            (await comercio.PostAsync($"/api/comercio/operadores/{DatabaseSeederSegundoOperador.OperadorId}/sesion", null)).StatusCode);

        // El personal del operador no usa estos endpoints
        var admin = await ClienteComoAsync("admin@gmail.com", "Admin.2026!");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/comercio/operadores")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await admin.PostAsync($"/api/comercio/operadores/{DatabaseSeeder.DefaultOperadorId}/sesion", null)).StatusCode);
    }
}
