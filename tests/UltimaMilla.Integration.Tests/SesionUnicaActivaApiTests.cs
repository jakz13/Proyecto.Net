using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Política de sesión única activa: SOLO para repartidores (app móvil en un único dispositivo). Un nuevo inicio de sesión de un repartidor deja sin efecto sus tokens
/// anteriores (401 SESION_DESPLAZADA); el resto de los perfiles puede tener varias sesiones a la vez con la misma cuenta (dos navegadores, cuentas de demostración).
/// La fábrica de integración la desactiva por defecto (las cuentas de prueba son compartidas); estas pruebas la activan con <c>Pruebas:SesionUnicaReal=true</c>.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class SesionUnicaActivaApiTests
{
    private const string RutaDeAdministracion = "/api/v1/flota/vehiculos";
    private const string RutaDelRepartidor = "/api/mobile/v1/rutas";
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

    /// <summary>Un repartidor propio (los tests comparten la base), creado por el administrador.</summary>
    private static async Task<string> CrearRepartidorAsync(HttpClient cliente)
    {
        var admin = await IniciarSesionAsync(cliente, "admin@gmail.com", "Admin.2026!");
        var email = $"sesion.{Guid.NewGuid().ToString("N")[..10]}@ultimamilla.com.uy";
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/api/v1/flota/repartidores")
        {
            Content = JsonContent.Create(new { nombreCompleto = "Repartidor sesion", email, documento = CedulaDePrueba.Nueva(), telefono = "099000222", password = "Repartidor.2026!" })
        };
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", admin);
        var alta = await cliente.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        return email;
    }

    [FactPostgres]
    public async Task UnNuevoInicioDeSesionDeUnRepartidor_DejaSinEfectoElTokenAnterior()
    {
        var cliente = ClienteConPoliticaReal();
        var email = await CrearRepartidorAsync(cliente);

        var primero = await IniciarSesionAsync(cliente, email, "Repartidor.2026!");
        var segundo = await IniciarSesionAsync(cliente, email, "Repartidor.2026!");

        var desplazada = await ConsultarAsync(cliente, primero, RutaDelRepartidor);
        Assert.Equal(HttpStatusCode.Unauthorized, desplazada.StatusCode);
        var cuerpo = await desplazada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SESION_DESPLAZADA", cuerpo.GetProperty("codigo").GetString());
        Assert.NotEqual(HttpStatusCode.Unauthorized, (await ConsultarAsync(cliente, segundo, RutaDelRepartidor)).StatusCode);
    }

    [FactPostgres]
    public async Task LaSesionDeUnRepartidor_NoAfectaALaDeOtroRepartidorNiALaDeAdministracion()
    {
        var cliente = ClienteConPoliticaReal();
        var uno = await CrearRepartidorAsync(cliente);
        var otro = await CrearRepartidorAsync(cliente);

        var admin = await IniciarSesionAsync(cliente, "admin@gmail.com", "Admin.2026!");
        var tokenUno = await IniciarSesionAsync(cliente, uno, "Repartidor.2026!");
        _ = await IniciarSesionAsync(cliente, otro, "Repartidor.2026!");
        _ = await IniciarSesionAsync(cliente, otro, "Repartidor.2026!");

        Assert.NotEqual(HttpStatusCode.Unauthorized, (await ConsultarAsync(cliente, tokenUno, RutaDelRepartidor)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ConsultarAsync(cliente, admin, RutaDeAdministracion)).StatusCode);
    }

    [FactPostgres]
    public async Task LosDemasPerfiles_PuedenTenerVariasSesionesALaVezConLaMismaCuenta()
    {
        var cuentas = new[]
        {
            ("admin@gmail.com", "Admin.2026!", RutaDeAdministracion),
            ("deposito@gmail.com", "Deposito.2026!", "/api/deposito/envios"),
            ("comercio@gmail.com", "Comercio.2026!", "/api/envios")
        };

        foreach (var (email, password, ruta) in cuentas)
        {
            var cliente = ClienteConPoliticaReal();
            var tokens = new[]
            {
                await IniciarSesionAsync(cliente, email, password),
                await IniciarSesionAsync(cliente, email, password),
                await IniciarSesionAsync(cliente, email, password)
            };

            // Ningún token queda desplazado: el middleware corre antes del enrutado, así que un 401 por sesión se vería aunque la ruta no exista
            foreach (var token in tokens)
            {
                var respuesta = await ConsultarAsync(cliente, token, ruta);
                var texto = await respuesta.Content.ReadAsStringAsync();
                Assert.False(texto.Contains("SESION_DESPLAZADA"), $"{email}: una sesión anterior quedó desplazada.");
                Assert.NotEqual(HttpStatusCode.Unauthorized, respuesta.StatusCode);
            }
        }
    }

    [FactPostgres]
    public async Task ElInicioDeSesionYLaSaludNoSeBloquean_AunqueHayaUnaSesionMasNueva()
    {
        var cliente = ClienteConPoliticaReal();
        var email = await CrearRepartidorAsync(cliente);
        var viejo = await IniciarSesionAsync(cliente, email, "Repartidor.2026!");
        _ = await IniciarSesionAsync(cliente, email, "Repartidor.2026!");

        // /ready y el login están exentos: un repartidor desplazado puede volver a iniciar sesión sin quedar bloqueado
        Assert.Equal(HttpStatusCode.OK, (await ConsultarAsync(cliente, viejo, "/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Repartidor.2026!" })).StatusCode);
    }

    [FactPostgres]
    public async Task ConLaFabricaDePruebasPorDefecto_VariosInicioDeSesionDeLaMismaCuentaNoSeExpulsan()
    {
        // Es lo que permite que las cuentas compartidas de los tests de integración convivan
        var cliente = _factory.CreateClient();
        var email = await CrearRepartidorAsync(cliente);

        var primero = await IniciarSesionAsync(cliente, email, "Repartidor.2026!");
        _ = await IniciarSesionAsync(cliente, email, "Repartidor.2026!");

        Assert.NotEqual(HttpStatusCode.Unauthorized, (await ConsultarAsync(cliente, primero, RutaDelRepartidor)).StatusCode);
    }
}
