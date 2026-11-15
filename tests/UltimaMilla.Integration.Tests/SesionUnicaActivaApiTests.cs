using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Política de sesión única activa por usuario: un nuevo inicio de sesión con la misma cuenta deja sin efecto los tokens anteriores (401 SESION_DESPLAZADA).
/// La fábrica de integración la desactiva por defecto (las cuentas de prueba son compartidas); estas pruebas la activan con <c>Pruebas:SesionUnicaReal=true</c>.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class SesionUnicaActivaApiTests
{
    private const string RutaDeAdministracion = "/api/v1/flota/vehiculos";
    private readonly ApiFactory _factory;

    public SesionUnicaActivaApiTests(ApiFactory factory) => _factory = factory;

    private static async Task<string> IniciarSesionAsync(HttpClient cliente, string email, string password)
    {
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    private static async Task<HttpResponseMessage> ConsultarAsync(HttpClient cliente, string token, string ruta)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, ruta);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await cliente.SendAsync(solicitud);
    }

    private HttpClient ClienteConPoliticaReal()
        => _factory.WithWebHostBuilder(b => b.UseSetting("Pruebas:SesionUnicaReal", "true")).CreateClient();

    [FactPostgres]
    public async Task UnNuevoInicioDeSesionDeLaMismaCuenta_DejaSinEfectoElTokenAnterior()
    {
        var cliente = ClienteConPoliticaReal();

        var primero = await IniciarSesionAsync(cliente, "admin@gmail.com", "Admin.2026!");
        Assert.Equal(HttpStatusCode.OK, (await ConsultarAsync(cliente, primero, RutaDeAdministracion)).StatusCode);

        var segundo = await IniciarSesionAsync(cliente, "admin@gmail.com", "Admin.2026!");

        var desplazada = await ConsultarAsync(cliente, primero, RutaDeAdministracion);
        Assert.Equal(HttpStatusCode.Unauthorized, desplazada.StatusCode);
        var cuerpo = await desplazada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SESION_DESPLAZADA", cuerpo.GetProperty("codigo").GetString());
        Assert.Equal(HttpStatusCode.OK, (await ConsultarAsync(cliente, segundo, RutaDeAdministracion)).StatusCode);
    }

    [FactPostgres]
    public async Task LaSesionDeUnaCuenta_NoAfectaALaDeOtraCuenta()
    {
        var cliente = ClienteConPoliticaReal();

        var admin = await IniciarSesionAsync(cliente, "admin@gmail.com", "Admin.2026!");
        _ = await IniciarSesionAsync(cliente, "deposito@gmail.com", "Deposito.2026!");
        _ = await IniciarSesionAsync(cliente, "deposito@gmail.com", "Deposito.2026!");

        Assert.Equal(HttpStatusCode.OK, (await ConsultarAsync(cliente, admin, RutaDeAdministracion)).StatusCode);
    }

    [FactPostgres]
    public async Task ElInicioDeSesionYLaSaludNoSeBloquean_AunqueHayaUnaSesionMasNueva()
    {
        var cliente = ClienteConPoliticaReal();
        var viejo = await IniciarSesionAsync(cliente, "comercio@gmail.com", "Comercio.2026!");
        _ = await IniciarSesionAsync(cliente, "comercio@gmail.com", "Comercio.2026!");

        // /ready y el login están exentos: un cliente desplazado puede volver a iniciar sesión sin quedar bloqueado
        Assert.Equal(HttpStatusCode.OK, (await ConsultarAsync(cliente, viejo, "/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = "comercio@gmail.com", password = "Comercio.2026!" })).StatusCode);
    }

    [FactPostgres]
    public async Task ConLaFabricaDePruebasPorDefecto_VariosInicioDeSesionDeLaMismaCuentaNoSeExpulsan()
    {
        // Es lo que permite que las cuentas compartidas de los tests de integración convivan
        var cliente = _factory.CreateClient();

        var primero = await IniciarSesionAsync(cliente, "admin@gmail.com", "Admin.2026!");
        _ = await IniciarSesionAsync(cliente, "admin@gmail.com", "Admin.2026!");

        Assert.Equal(HttpStatusCode.OK, (await ConsultarAsync(cliente, primero, RutaDeAdministracion)).StatusCode);
    }
}
