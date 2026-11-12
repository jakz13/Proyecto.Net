using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UltimaMilla.Infrastructure.Messaging;
using UltimaMilla.Infrastructure.Services;

namespace UltimaMilla.Integration.Tests;

/// <summary>Correcciones de la auditoría de seguridad: cada una tiene una prueba que fallaba antes del arreglo.</summary>
[Collection(ColeccionApi.Nombre)]
public class SeguridadEndurecimientoTests
{
    private readonly ApiFactory _factory;

    public SeguridadEndurecimientoTests(ApiFactory factory) => _factory = factory;

    // ----- Marca de un operador cualquiera, sin sesión -----

    [FactPostgres]
    public async Task LaMarcaDeUnOperadorPorSuIdentificador_NoSeEntregaSinSesionFueraDelModoDemo()
    {
        var servidor = _factory.WithWebHostBuilder(b => b.UseSetting("Auth:PermitirModoDemo", "false"));
        var cliente = servidor.CreateClient();

        var respuesta = await cliente.GetAsync("/api/v1/identidad-visual/operadores-demo/11111111-1111-1111-1111-111111111111");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ----- SSRF en los avisos a comercios -----

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.18.0.5")]          // red de Docker: base de datos, cola, Redis
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")]     // metadatos de la nube
    [InlineData("100.64.0.1")]
    [InlineData("::1")]
    [InlineData("fd00::1")]
    [InlineData("fe80::1")]
    [InlineData("::ffff:10.0.0.1")]     // IPv4 disfrazada de IPv6
    public void LasDireccionesInternasSeRechazanComoDestinoDeAvisos(string ip) =>
        Assert.True(DestinoWebhookSeguro.EsDireccionInterna(System.Net.IPAddress.Parse(ip)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("152.70.219.103")]
    [InlineData("172.32.0.1")]          // fuera del rango privado 172.16.0.0/12
    [InlineData("2606:4700:4700::1111")]
    public void LasDireccionesPublicasSeAceptan(string ip) =>
        Assert.False(DestinoWebhookSeguro.EsDireccionInterna(System.Net.IPAddress.Parse(ip)));

    [Fact]
    public async Task ElEnvioDeUnAvisoHaciaLocalhostFallaSinLlegarAlDestino()
    {
        using var manejador = new SocketsHttpHandler
        {
            ConnectCallback = DestinoWebhookSeguro.CrearConexion(false, new HashSet<string>())
        };
        using var http = new HttpClient(manejador);
        var emisor = new HttpWebhookSender(http);

        var resultado = await emisor.EnviarAsync("http://127.0.0.1:5000/api/testing/webhook-receiver", "{}", "firma", Guid.NewGuid());

        Assert.False(resultado.Exitoso);
        Assert.Contains("interna", resultado.Detalle ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    // ----- JWT -----

    private static string TokenSinVencimiento(string clave)
    {
        static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var cabecera = B64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var cuerpo = B64(Encoding.UTF8.GetBytes("{\"sub\":\"1\",\"role\":\"Administrador\"}"));
        var firma = B64(HMACSHA256.HashData(Encoding.UTF8.GetBytes(clave), Encoding.UTF8.GetBytes($"{cabecera}.{cuerpo}")));
        return $"{cabecera}.{cuerpo}.{firma}";
    }

    [Fact]
    public void UnTokenFirmadoPeroSinVencimientoNoEsValido()
    {
        var token = TokenSinVencimiento(TokenJwtService.ClaveSecretaPorDefecto);
        Assert.Null(TokenJwtService.ValidarYExtraerClaims(token, TokenJwtService.ClaveSecretaPorDefecto));
    }

    [Fact]
    public void UnTokenConLaFirmaAlteradaNoEsValido()
    {
        var token = TokenSinVencimiento("otra-clave-distinta-de-mas-de-32-caracteres!!");
        Assert.Null(TokenJwtService.ValidarYExtraerClaims(token, TokenJwtService.ClaveSecretaPorDefecto));
    }

    [Fact]
    public void EnProduccionNoSeUsaLaClaveDeDesarrolloDelRepositorio()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ASPNETCORE_ENVIRONMENT"] = "Production" })
            .Build();
        object Crear() => new TokenJwtService(config);
        Assert.Throws<InvalidOperationException>(Crear);
    }

    // ----- Archivos de logo y datos públicos -----

    [Theory]
    [InlineData("..%2f..%2fappsettings.json")]
    [InlineData("logo.svg")]
    [InlineData("logo.html")]
    [InlineData("C%3A%5Cwindows%5Cwin.ini")]
    [InlineData("sin-extension")]
    public async Task ElServicioDeLogosRechazaRutasYTiposNoPermitidos(string nombre)
    {
        var respuesta = await _factory.CreateClient().GetAsync($"/api/v1/identidad-visual/logos/{nombre}");
        Assert.True(respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound, $"Se obtuvo {(int)respuesta.StatusCode}");
    }

    // ----- Alta de repartidores sin contraseña por defecto -----

    [FactPostgres]
    public async Task ElAltaDeUnRepartidorExigeUnaContrasenaProvisoria_NoExisteUnaPorDefecto()
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = "admin@gmail.com", password = "Admin.2026!" });
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());

        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var sinPassword = await cliente.PostAsJsonAsync("/api/v1/flota/repartidores", new
        {
            nombreCompleto = "Sin clave " + sufijo, email = $"sc.{sufijo}@ultimamilla.com.uy", documento = "1.111.111-1", telefono = "099111111"
        });
        Assert.Equal(HttpStatusCode.BadRequest, sinPassword.StatusCode);

        var clavesCorta = await cliente.PostAsJsonAsync("/api/v1/flota/repartidores", new
        {
            nombreCompleto = "Corta " + sufijo, email = $"co.{sufijo}@ultimamilla.com.uy", documento = "1.111.111-1", telefono = "099111111", password = "123"
        });
        Assert.Equal(HttpStatusCode.BadRequest, clavesCorta.StatusCode);
    }
}
