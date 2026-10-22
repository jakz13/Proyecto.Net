using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Application.Commands.Identidad.IniciarSesion;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Pruebas de integración sobre el endpoint de autenticación y emisión de tokens (UC-40)
/// verificando respuestas HTTP, emisión de token JWT y control de fuerza bruta.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class AutenticacionApiTests
{
    private readonly ApiFactory _factory;

    public AutenticacionApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [FactPostgres]
    public async Task Login_CredencialesValidas_Retorna200ConTokenJWT()
    {
        var cliente = _factory.CreateClient();

        var request = new IniciarSesionRequest("admin@gmail.com", "Admin.2026!");
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/auth/login", request);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var resultado = await respuesta.Content.ReadFromJsonAsync<IniciarSesionResultadoDto>();
        Assert.NotNull(resultado);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Token));
        Assert.Equal("admin@gmail.com", resultado.Email);
        Assert.Equal("Administrador", resultado.Perfil);
        Assert.NotNull(resultado.OperadorId);

        // Usar el token emitido para llamar a un endpoint protegido
        var requestProtegido = new HttpRequestMessage(HttpMethod.Get, "/api/mobile/v1/ping");
        requestProtegido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resultado.Token);
        var respuestaProtegida = await cliente.SendAsync(requestProtegido);

        Assert.Equal(HttpStatusCode.OK, respuestaProtegida.StatusCode);
    }

    [FactPostgres]
    public async Task Login_OperadorDos_Retorna200ConOperadorCorrespondiente()
    {
        var cliente = _factory.CreateClient();

        var request = new IniciarSesionRequest("admin2@gmail.com", "Admin.2026!");
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/auth/login", request);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var resultado = await respuesta.Content.ReadFromJsonAsync<IniciarSesionResultadoDto>();
        Assert.NotNull(resultado);
        Assert.Equal("admin2@gmail.com", resultado.Email);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000002"), resultado.OperadorId);
    }

    [FactPostgres]
    public async Task Login_ContrasenaIncorrecta_Retorna401Unauthorized()
    {
        var cliente = _factory.CreateClient();

        var request = new IniciarSesionRequest("admin@gmail.com", "PasswordErroneo!");
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/auth/login", request);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [FactPostgres]
    public async Task Login_SuperarMaximoIntentos_Retorna423Locked()
    {
        var cliente = _factory.CreateClient();
        var email = "despacho@gmail.com";

        // Realizar 5 intentos fallidos
        for (int i = 1; i <= 4; i++)
        {
            var res = await cliente.PostAsJsonAsync("/api/v1/auth/login", new IniciarSesionRequest(email, $"Erroneo_{i}"));
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        // El 5to intento bloquea la cuenta y responde 423 Locked
        var respuestaBloqueo = await cliente.PostAsJsonAsync("/api/v1/auth/login", new IniciarSesionRequest(email, "Erroneo_5"));
        Assert.Equal(HttpStatusCode.Locked, respuestaBloqueo.StatusCode);
    }
}
