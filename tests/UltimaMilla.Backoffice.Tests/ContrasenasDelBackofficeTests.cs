using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using UltimaMilla.Application.Common;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// Al dar de alta un repartidor o un usuario, el servidor rechazaba la contraseña recién al enviar (mínimo 10 caracteres y tres tipos de caracteres, mientras el
/// formulario decía "mínimo 8"), el panel se cerraba y había que reabrirlo. Ahora el formulario mide la contraseña mientras se escribe, ofrece el ojito para verla y no
/// deja enviar si no cumple (password-ui.js, con la misma regla que PoliticaDePassword), y tras un error del servidor el panel sigue abierto.
/// </summary>
[Collection("Backoffice")]
public class ContrasenasDelBackofficeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Regex Token = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly WebApplicationFactory<Program> _fabrica;

    public ContrasenasDelBackofficeTests(WebApplicationFactory<Program> fabrica)
        => _fabrica = fabrica.WithWebHostBuilder(constructor => constructor.UseSetting("UsarMocks", "true"));

    private static string Raiz()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.prod.yml")))
            carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);
        return carpeta!.FullName;
    }

    private async Task<HttpClient> SesionDeAdministradorAsync()
    {
        var cliente = _fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var html = await (await cliente.GetAsync("/Login")).Content.ReadAsStringAsync();
        var respuesta = await cliente.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RolSeleccionado"] = "Administrador",
            ["NombreUsuario"] = "Prueba Administrador",
            ["__RequestVerificationToken"] = Token.Match(html).Groups[1].Value
        }));
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        return cliente;
    }

    [Fact]
    public void LaReglaDelFormulario_TieneElMismoLargoMinimoYLasMismasContrasenasTrivialesQueElServidor()
    {
        var js = File.ReadAllText(Path.Combine(Raiz(), "src/Web/Backoffice/wwwroot/js/password-ui.js"));

        Assert.Contains($"var LONGITUD_MINIMA = {PoliticaDePassword.LongitudMinima};", js);

        var campo = typeof(PoliticaDePassword).GetField("Triviales", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(campo);
        var triviales = ((IEnumerable<string>)campo!.GetValue(null)!).ToList();
        Assert.NotEmpty(triviales);
        foreach (var trivial in triviales)
            Assert.Contains($"'{trivial.ToLowerInvariant()}'", js);
    }

    [Theory]
    [InlineData("src/Web/Backoffice/Pages/Flota/Index.cshtml", 2)]
    [InlineData("src/Web/Backoffice/Pages/Usuarios/Nuevo.cshtml", 1)]
    public void LosCamposDeContrasena_UsanElAyudanteYNoPrometenUnMinimoDe8(string pagina, int campos)
    {
        var html = File.ReadAllText(Path.Combine(Raiz(), pagina));

        Assert.Contains("~/js/password-ui.js", html);
        Assert.Equal(campos, Regex.Matches(html, "type=\"password\"[^>]*data-politica-password").Count);
        Assert.DoesNotContain("Mínimo 8 caracteres", html);
        Assert.DoesNotContain("minlength=\"8\"", html);
    }

    [Fact]
    public async Task Flota_ConUnAltaRechazadaPorElServidor_ElPanelSigueAbierto()
    {
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/Flota")).Content.ReadAsStringAsync();

        var respuesta = await cliente.PostAsync("/Flota?handler=CrearRepartidor", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NuevoRepartidor.NombreCompleto"] = "",
            ["__RequestVerificationToken"] = Token.Match(html).Groups[1].Value
        }));

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("var panelAbierto = document.getElementById('panelRepartidorNuevo')", cuerpo);
    }

    [Fact]
    public async Task Flota_SinErrores_NoAbreNingunPanelAlCargar()
    {
        var cliente = await SesionDeAdministradorAsync();
        var cuerpo = await (await cliente.GetAsync("/Flota")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("var panelAbierto", cuerpo);
    }
}
