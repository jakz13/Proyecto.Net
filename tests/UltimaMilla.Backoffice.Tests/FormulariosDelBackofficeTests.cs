using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// Formularios del Backoffice que fallaban sin que ninguna prueba lo notara: se ejercitan como lo haría el navegador.
/// </summary>
public class FormulariosDelBackofficeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Regex Token = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly WebApplicationFactory<Program> _fabrica;

    public FormulariosDelBackofficeTests(WebApplicationFactory<Program> fabrica)
    {
        _fabrica = fabrica;
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
    public async Task ZonaNueva_LasFranjasSugeridasSeMuestranEnFormato24Horas()
    {
        // Con el formato "hh" (12 horas) 13:00 se mostraba como 01:00 y la zona no se podía guardar sin corregir las franjas
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/Zonas/Nueva")).Content.ReadAsStringAsync();

        var horas = Regex.Matches(html, "<input type=\"time\" name=\"Franjas\\[\\d+\\]\\.Hora(?:Inicio|Fin)\" value=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(new[] { "09:00", "13:00", "14:00", "18:00" }, horas);
    }

    [Fact]
    public async Task MotivosNoEntrega_AltaDeUnMotivo_NoSeRechazaPorLosCamposDelFormularioDeEdicion()
    {
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/MotivosNoEntrega")).Content.ReadAsStringAsync();

        var respuesta = await cliente.PostAsync("/MotivosNoEntrega?handler=Crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NuevoMotivo.Codigo"] = "MOTIVO_PRUEBA_" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            ["NuevoMotivo.Descripcion"] = "Motivo de prueba",
            ["NuevoMotivo.IniciarActivo"] = "true",
            ["__RequestVerificationToken"] = Token.Match(html).Groups[1].Value
        }));

        // Alta correcta: redirige al listado; si se rechazara por validación volvería a pintar la página con el error (200)
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        var aviso = Regex.Match(cuerpo, "(Por favor[^<]{0,400}|Error al[^<]{0,160})").Groups[1].Value + " / HTTP " + (int)respuesta.StatusCode;
        Assert.True(respuesta.StatusCode == HttpStatusCode.Redirect, "No redirigió: " + aviso);
    }
}
