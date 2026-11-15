using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// Armar una hoja de ruta: el vehículo es el que el repartidor tiene asignado (se muestra, no se elige) y el Tablero no ofrece "Actualizar" porque se actualiza solo.
/// Alta de usuario: el comercio solo se asocia al administrador de comercio y el campo se llama "Comercio Asociado".
/// </summary>
[Collection("Backoffice")]
public class PantallasDeArmadoYUsuariosTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Regex Token = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly WebApplicationFactory<Program> _fabrica;

    public PantallasDeArmadoYUsuariosTests(WebApplicationFactory<Program> fabrica)
        => _fabrica = fabrica.WithWebHostBuilder(constructor => constructor.UseSetting("UsarMocks", "true"));

    private async Task<string> PaginaComoAsync(string perfil, string ruta)
    {
        var cliente = _fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var login = await (await cliente.GetAsync("/Login")).Content.ReadAsStringAsync();
        var entrada = await cliente.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RolSeleccionado"] = perfil,
            ["NombreUsuario"] = "Prueba",
            ["__RequestVerificationToken"] = Token.Match(login).Groups[1].Value
        }));
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);

        var pagina = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        return await pagina.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task ArmarHojaDeRuta_MuestraElVehiculoAsignadoSinPermitirElegirOtro()
    {
        var html = await PaginaComoAsync("Despachador", "/Rutas/Armar");

        Assert.Contains("Vehículo asignado", html);
        Assert.DoesNotContain("id=\"selectVehiculo\"", html);
        Assert.DoesNotContain("Vehículo de la Flota", html);
        Assert.Contains("name=\"Formulario.VehiculoId\"", html);   // viaja oculto, no como lista
    }

    [Fact]
    public async Task ElTableroNoOfreceActualizarPorqueSeActualizaSolo()
    {
        var html = await PaginaComoAsync("Despachador", "/Tablero");

        Assert.DoesNotContain("TableroTiempoReal.actualizar()", html);
        Assert.Contains("estadoConexionTiempoReal", html);
    }

    [Fact]
    public async Task AltaDeUsuario_LlamaComercioAsociadoAlCampo_YLoOcultaHastaElegirAdministradorDeComercio()
    {
        var html = await PaginaComoAsync("Administrador", "/Usuarios/Nuevo");

        Assert.Contains("Comercio Asociado", html);
        Assert.DoesNotContain("Comercio Cliente Asociado", html);
        Assert.Matches("id=\"bloqueComercioAsociado\"[^>]*display: none", html);
    }
}
