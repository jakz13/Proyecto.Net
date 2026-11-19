using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// Con varias sesiones abiertas a la vez (dos navegadores con la misma cuenta, o varios usuarios del operador), un cambio hecho en una debe verse solo en las demás.
/// Antes solo 11 de las 42 pantallas se actualizaban solas, y únicamente ante cambios de estado de envíos y rutas. Ahora la API avisa de TODA escritura
/// (<c>DatosCambiaron</c>) y todas las pantallas del Backoffice la escuchan, salvo las de alta o edición, donde hay formularios armados con JavaScript.
/// </summary>
[Collection("Backoffice")]
public class TiempoRealDeTodasLasPantallasTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Regex Token = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly WebApplicationFactory<Program> _fabrica;

    public TiempoRealDeTodasLasPantallasTests(WebApplicationFactory<Program> fabrica)
        => _fabrica = fabrica.WithWebHostBuilder(constructor => constructor.UseSetting("UsarMocks", "true"));

    private static string Raiz()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.prod.yml")))
            carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);
        return carpeta!.FullName;
    }

    private async Task<HttpClient> SesionComoAsync(string perfil)
    {
        var cliente = _fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var html = await (await cliente.GetAsync("/Login")).Content.ReadAsStringAsync();
        var respuesta = await cliente.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RolSeleccionado"] = perfil,
            ["NombreUsuario"] = "Prueba " + perfil,
            ["__RequestVerificationToken"] = Token.Match(html).Groups[1].Value
        }));
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        return cliente;
    }

    [Theory]
    [InlineData("/Flota", true)]
    [InlineData("/Zonas", true)]
    [InlineData("/Tarifas", true)]
    [InlineData("/ReglasOperativas", true)]
    [InlineData("/MotivosNoEntrega", true)]
    [InlineData("/Comercios", true)]
    [InlineData("/Mensajeria", true)]
    [InlineData("/Zonas/Nueva", false)]
    [InlineData("/Tarifas/NuevaVersion", false)]
    [InlineData("/ReglasOperativas/NuevaVersion", false)]
    [InlineData("/Usuarios/Nuevo", false)]
    [InlineData("/Comercios/Nuevo", false)]
    public async Task LasPantallasDeListado_SeActualizanSolas_YLasDeAltaOEdicionNo(string ruta, bool seActualiza)
    {
        var cliente = await SesionComoAsync("Administrador");

        var respuesta = await cliente.GetAsync(ruta);
        var html = await respuesta.Content.ReadAsStringAsync();

        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"{ruta} respondió {(int)respuesta.StatusCode}");
        Assert.Contains($"data-tiempo-real-envios=\"{(seActualiza ? "true" : "false")}\"", html);
    }

    [Fact]
    public void ElScriptDelBackoffice_EscuchaElAvisoDeDatosCambiadosYActualizaLosSelectores()
    {
        var js = File.ReadAllText(Path.Combine(Raiz(), "src/Web/Backoffice/wwwroot/js/tiempo-real-envios.js"));

        Assert.Contains("conexion.on('DatosCambiaron'", js);
        Assert.Contains("function sincronizarOpciones", js);
        Assert.Contains("viejo.tagName === 'SELECT'", js);
    }

    [Fact]
    public void ElPortalDelComercio_EscuchaElMismoAvisoYRefrescaSuLista()
    {
        var js = File.ReadAllText(Path.Combine(Raiz(), "src/Web/PortalComercio/wwwroot/js/tiempo-real-envios.js"));

        Assert.Matches(@"conexion\.on\('DatosCambiaron',[\s\S]*?AlCambiarEstado", js);
    }

    [Fact]
    public void ElNombreDelEventoEsElMismoEnElServidorYEnLosClientes()
    {
        var servidor = File.ReadAllText(Path.Combine(Raiz(), "src/Api/Services/CambiosEnTiempoRealMiddleware.cs"));

        Assert.Contains("EventoDatosCambiaron = \"DatosCambiaron\"", servidor);
    }

    [Fact]
    public async Task ElTokenDeTiempoReal_ConservaElMismoSidEnTodaLaSesion_ParaNoSerRechazadoPorTableroHub()
    {
        var cliente = await SesionComoAsync("Despachador");

        var respuesta1 = await cliente.GetAsync("/tiempo-real/token");
        var respuesta2 = await cliente.GetAsync("/tiempo-real/token");
        Assert.Equal(HttpStatusCode.OK, respuesta1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, respuesta2.StatusCode);

        using var doc1 = System.Text.Json.JsonDocument.Parse(await respuesta1.Content.ReadAsStringAsync());
        using var doc2 = System.Text.Json.JsonDocument.Parse(await respuesta2.Content.ReadAsStringAsync());
        var token1 = doc1.RootElement.GetProperty("accessToken").GetString();
        var token2 = doc2.RootElement.GetProperty("accessToken").GetString();

        var sid1 = UltimaMilla.Backoffice.Seguridad.EmisorTokenTiempoReal.ExtraerSessionIdDeJwt(token1);
        var sid2 = UltimaMilla.Backoffice.Seguridad.EmisorTokenTiempoReal.ExtraerSessionIdDeJwt(token2);

        Assert.False(string.IsNullOrWhiteSpace(sid1));
        Assert.Equal(sid1, sid2);
    }

    [Fact]
    public void ElTokenDeTiempoReal_ExtraeElSidDelJwtDeLaApi_CuandoLaCookieNoTieneClaimSidDirecto()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var servicioJwt = new UltimaMilla.Infrastructure.Services.TokenJwtService(config);
        var usuarioId = Guid.NewGuid();
        var operadorId = UltimaMilla.Infrastructure.Persistence.DatabaseSeeder.DefaultOperadorId;
        var perfilId = UltimaMilla.Infrastructure.Persistence.DatabaseSeeder.DefaultPerfilDespachadorId;
        var usuario = new UltimaMilla.Domain.Entities.Identidad.Usuario(usuarioId, "Despachador", "desp@test.uy", "hash", perfilId, operadorId: operadorId);
        var perfil = new UltimaMilla.Domain.Entities.Identidad.Perfil(perfilId, "Despachador", "Despachador");

        var sidEsperado = "sesion-api-fija-123456";
        var jwtApi = servicioJwt.GenerarToken(usuario, perfil, new[] { "RUTAS_DESPACHO" }, TimeSpan.FromHours(1), sessionId: sidEsperado);

        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, usuarioId.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Despachador"),
            new System.Security.Claims.Claim("operador_id", operadorId.ToString()),
            new System.Security.Claims.Claim(UltimaMilla.Backoffice.Seguridad.TokenApiHandler.ClaimToken, jwtApi.Token)
        }, "TestAuth"));

        var emitido = UltimaMilla.Backoffice.Seguridad.EmisorTokenTiempoReal.Emitir(principal, config, null, "/hubs/tablero");
        var json = System.Text.Json.JsonSerializer.Serialize(emitido);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var accessToken = doc.RootElement.GetProperty("accessToken").GetString();

        var sidEmitido = UltimaMilla.Backoffice.Seguridad.EmisorTokenTiempoReal.ExtraerSessionIdDeJwt(accessToken);
        Assert.Equal(sidEsperado, sidEmitido);
    }
}
