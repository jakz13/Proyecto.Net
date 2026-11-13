using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// Formularios del Backoffice que fallaban sin que ninguna prueba lo notara: se ejercitan como lo haría el navegador.
/// </summary>
// Las dos clases levantan el Backoffice en el mismo proceso: en un equipo limpio (CI) compiten por crear las claves de protección de datos
// y las sesiones y los tokens antiforgery de una dejan de valer para la otra. Se ejecutan una tras otra.
[Collection("Backoffice")]
public class FormulariosDelBackofficeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Regex Token = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly WebApplicationFactory<Program> _fabrica;

    public FormulariosDelBackofficeTests(WebApplicationFactory<Program> fabrica)
    {
        _fabrica = fabrica;
    }

    private Task<HttpClient> SesionDeAdministradorAsync() => SesionComoAsync("Administrador");

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

    [Fact]
    public async Task Recepcion_SiElOperarioDesmarcaTodosLosBultos_NoSeDaElEnvioPorRecibido()
    {
        // Antes, una lista vacía se interpretaba como "llegaron todos" y el envío quedaba En Depósito sin discrepancias
        var cliente = await SesionComoAsync("Operario");
        var muelle = await (await cliente.GetAsync("/Deposito")).Content.ReadAsStringAsync();
        var envioId = Regex.Match(muelle, @"Deposito/Recepcion\?id=([0-9a-f-]{36})").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(envioId), "El muelle no lista ningún envío admitido.");
        var pagina = await cliente.GetAsync($"/Deposito/Recepcion?id={envioId}");
        var html = await pagina.Content.ReadAsStringAsync();
        Assert.True(pagina.StatusCode == HttpStatusCode.OK, "GET recepción: " + (int)pagina.StatusCode + " " + pagina.Headers.Location);

        var respuesta = await cliente.PostAsync($"/Deposito/Recepcion?id={envioId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Formulario.EnvioId"] = envioId,
            ["Formulario.CodigosEscaneadosCsv"] = "",
            ["Formulario.CodigosDanadosCsv"] = "",
            ["Formulario.Observaciones"] = "",
            ["__RequestVerificationToken"] = Token.Match(html).Groups[1].Value
        }));

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("No hay ningún bulto marcado", WebUtility.HtmlDecode(cuerpo));
    }
}
