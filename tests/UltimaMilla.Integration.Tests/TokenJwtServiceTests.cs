using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Infrastructure.Services;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Validación del token JWT propio: firma, algoritmo fijo, vencimiento, vigencia desde, emisor y audiencia.
/// La clave se pasa explícita a la validación: no se toca la clave global que usa la API de las demás pruebas.
/// </summary>
public class TokenJwtServiceTests
{
    private const string Clave = "ClaveDePruebaParaElTokenJwtDeLasPruebas2026!Larga";

    private static IConfiguration Configuracion() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SecretKey"] = Clave })
        .Build();

    private static string Base64Url(byte[] datos) => Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Firmar(object encabezado, object payload, string clave = Clave)
    {
        var h = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(encabezado)));
        var p = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(clave));
        return $"{h}.{p}.{Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{h}.{p}")))}";
    }

    private static object Encabezado(string alg = "HS256") => new { alg, typ = "JWT" };

    private static Dictionary<string, object> Payload(string iss = "UltimaMilla.Api", string aud = "UltimaMilla.Clients", long? exp = null, long? nbf = null)
    {
        var ahora = DateTimeOffset.UtcNow;
        var payload = new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString(),
            ["role"] = "Administrador",
            ["iss"] = iss,
            ["aud"] = aud,
            ["exp"] = exp ?? ahora.AddHours(1).ToUnixTimeSeconds()
        };
        if (nbf is not null)
            payload["nbf"] = nbf.Value;
        return payload;
    }

    private static string Validar(string token) => TokenJwtService.ValidarYExtraerClaims(token, Clave) is null ? "rechazado" : "aceptado";

    [Fact]
    public void UnTokenEmitidoPorElServicio_SeAcepta()
    {
        var servicio = new TokenJwtService(Configuracion());
        var usuario = new Usuario(Guid.NewGuid(), "Ana", "ana@correo.com", "hash", Guid.NewGuid(), Guid.NewGuid());
        var token = servicio.GenerarToken(usuario, new Perfil(usuario.PerfilId, "Administrador", ""), new[] { "X" });

        Assert.Equal("aceptado", Validar(token.Token));
    }

    [Fact]
    public void LaFirmaDeOtraClave_SeRechaza()
    {
        Assert.Equal("rechazado", Validar(Firmar(Encabezado(), Payload(), clave: "OtraClaveDistintaPeroLargaLargaLarga2026!!")));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("HS512")]
    [InlineData("RS256")]
    public void UnAlgoritmoQueNoEsHs256_SeRechaza(string alg)
    {
        Assert.Equal("rechazado", Validar(Firmar(Encabezado(alg), Payload())));
    }

    [Fact]
    public void UnTokenVencido_SeRechaza()
    {
        Assert.Equal("rechazado", Validar(Firmar(Encabezado(), Payload(exp: DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds()))));
    }

    [Fact]
    public void UnTokenQueTodaviaNoEsValido_SeRechaza_PeroConUnMinutoDeTolerancia()
    {
        Assert.Equal("rechazado", Validar(Firmar(Encabezado(), Payload(nbf: DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()))));
        Assert.Equal("aceptado", Validar(Firmar(Encabezado(), Payload(nbf: DateTimeOffset.UtcNow.AddSeconds(20).ToUnixTimeSeconds()))));
    }

    [Fact]
    public void UnTokenDeOtroEmisorOAudiencia_SeRechazaAunqueEsteBienFirmado()
    {
        Assert.Equal("rechazado", Validar(Firmar(Encabezado(), Payload(iss: "OtroSistema"))));
        Assert.Equal("rechazado", Validar(Firmar(Encabezado(), Payload(aud: "OtrosClientes"))));
    }

    [Fact]
    public void UnTokenSinEmisorNiAudiencia_SeRechaza()
    {
        var sinEmisor = Payload();
        sinEmisor.Remove("iss");
        var sinAudiencia = Payload();
        sinAudiencia.Remove("aud");

        Assert.Equal("rechazado", Validar(Firmar(Encabezado(), sinEmisor)));
        Assert.Equal("rechazado", Validar(Firmar(Encabezado(), sinAudiencia)));
    }

    [Fact]
    public void UnPayloadAlteradoDespuesDeFirmar_SeRechaza()
    {
        var token = Firmar(Encabezado(), Payload());
        var partes = token.Split('.');
        var alterado = Payload();
        alterado["role"] = "AdministradorPlataforma";
        var payloadAlterado = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(alterado)));

        Assert.Equal("rechazado", Validar($"{partes[0]}.{payloadAlterado}.{partes[2]}"));
    }
}
