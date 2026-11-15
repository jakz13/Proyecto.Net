using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// El administrador de la plataforma no pertenece a ningún operador: su menú no debe mostrar el nombre, el logo ni las etiquetas "Operador" y "Backoffice"
/// de uno de ellos, ni la entrada "Inicio" (solo redirige), y al cambiar una bandera no hay cartel de confirmación (se da por hecho).
/// </summary>
[Collection("Backoffice")]
public class MenuDelAdministradorDePlataformaTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Regex Token = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly WebApplicationFactory<Program> _fabrica;

    public MenuDelAdministradorDePlataformaTests(WebApplicationFactory<Program> fabrica)
        => _fabrica = fabrica.WithWebHostBuilder(constructor => constructor.UseSetting("UsarMocks", "true"));

    private async Task<string> PantallaDeFuncionalidadesComoAsync(string perfil)
    {
        var cliente = _fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var login = await (await cliente.GetAsync("/Login")).Content.ReadAsStringAsync();
        var respuesta = await cliente.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RolSeleccionado"] = perfil,
            ["NombreUsuario"] = "Prueba",
            ["__RequestVerificationToken"] = Token.Match(login).Groups[1].Value
        }));
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);

        var pagina = await cliente.GetAsync(perfil == "AdministradorPlataforma" ? "/Plataforma/Funcionalidades" : "/Index");
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        return await pagina.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task ElMenuDeLaPlataforma_DiceAdministradorDePlataforma_SinDatosDeUnOperador()
    {
        var html = await PantallaDeFuncionalidadesComoAsync("AdministradorPlataforma");

        Assert.Contains("Administrador de plataforma", html);
        Assert.DoesNotContain("Backoffice</span>", html);
        Assert.DoesNotContain(">Operador</span>", html);
        Assert.DoesNotContain("sidebarLogoImg", html);
        Assert.DoesNotContain("title=\"Inicio\"", html);
        Assert.DoesNotContain("alert-success", html);
    }

    [Fact]
    public async Task ElMenuDeUnOperador_SigueMostrandoSuMarcaYElInicio()
    {
        var html = await PantallaDeFuncionalidadesComoAsync("Administrador");

        Assert.Contains(">Operador</span>", html);
        Assert.Contains("title=\"Inicio\"", html);
        Assert.DoesNotContain("Administrador de plataforma", html);
    }
}
