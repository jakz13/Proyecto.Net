using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Backoffice;
using UltimaMilla.Frontend.Infrastructure.Excepciones;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// Con la política de sesión única activa la API rechaza con 401 SESION_DESPLAZADA el token de una cuenta que inició sesión en otro lugar. El Backoffice no
/// lo reconocía: la excepción llegaba a la página de error (HTTP 500) en el navegador desplazado (visto en producción el 15/11 con dos navegadores y la misma cuenta).
/// </summary>
[Collection("Backoffice")]
public class SesionDesplazadaTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CuerpoDeLaApi = "{\"error\":\"Tu sesión se cerró porque se inició sesión con esta cuenta en otro dispositivo.\",\"codigo\":\"SESION_DESPLAZADA\"}";
    private readonly WebApplicationFactory<Program> _fabrica;

    public SesionDesplazadaTests(WebApplicationFactory<Program> fabrica)
        => _fabrica = fabrica.WithWebHostBuilder(constructor => constructor.UseSetting("UsarMocks", "true"));

    private sealed class AutenticacionFalsa : IAuthenticationService
    {
        public int CierresDeSesion { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) { CierresDeSesion++; return Task.CompletedTask; }
    }

    private static (DefaultHttpContext Contexto, AutenticacionFalsa Autenticacion) NuevoContexto(string? secFetchMode = null)
    {
        var autenticacion = new AutenticacionFalsa();
        var servicios = new ServiceCollection().AddSingleton<IAuthenticationService>(autenticacion).BuildServiceProvider();
        var contexto = new DefaultHttpContext { RequestServices = servicios };
        contexto.Response.Body = new MemoryStream();
        if (secFetchMode is not null)
            contexto.Request.Headers["Sec-Fetch-Mode"] = secFetchMode;
        return (contexto, autenticacion);
    }

    [Fact]
    public async Task UnaNavegacionConLaSesionDesplazada_CierraLaSesionLocalYVuelveAlLoginConElMotivo()
    {
        var (contexto, autenticacion) = NuevoContexto("navigate");
        var middleware = new SesionDesplazadaMiddleware(_ => throw new ApiExcepcion(HttpStatusCode.Unauthorized, CuerpoDeLaApi));

        await middleware.InvokeAsync(contexto);

        Assert.Equal(StatusCodes.Status302Found, contexto.Response.StatusCode);
        Assert.Equal("/Login?motivo=sesion-desplazada", contexto.Response.Headers.Location.ToString());
        Assert.Equal(1, autenticacion.CierresDeSesion);
    }

    [Fact]
    public async Task UnFetchConLaSesionDesplazada_RecibeUn401ConElCodigoYNoUnaRedireccion()
    {
        var (contexto, autenticacion) = NuevoContexto("cors");
        var middleware = new SesionDesplazadaMiddleware(_ => throw new ApiExcepcion(HttpStatusCode.Unauthorized, CuerpoDeLaApi));

        await middleware.InvokeAsync(contexto);

        Assert.Equal(StatusCodes.Status401Unauthorized, contexto.Response.StatusCode);
        contexto.Response.Body.Position = 0;
        Assert.Contains("SESION_DESPLAZADA", await new StreamReader(contexto.Response.Body).ReadToEndAsync());
        Assert.Equal(1, autenticacion.CierresDeSesion);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{\"error\":\"Token inválido\"}")]     // otro 401: no es una sesión desplazada
    [InlineData(HttpStatusCode.Conflict, CuerpoDeLaApi)]                            // otro estado con el mismo texto
    [InlineData(HttpStatusCode.BadRequest, "dato inválido")]
    public async Task CualquierOtroErrorDeLaApi_NoSeTraga(HttpStatusCode estado, string cuerpo)
    {
        var (contexto, autenticacion) = NuevoContexto("navigate");
        var middleware = new SesionDesplazadaMiddleware(_ => throw new ApiExcepcion(estado, cuerpo));

        await Assert.ThrowsAsync<ApiExcepcion>(() => middleware.InvokeAsync(contexto));
        Assert.Equal(0, autenticacion.CierresDeSesion);
    }

    [Fact]
    public void ElMiddleware_EstaRegistradoDespuesDeLaAutorizacionYAntesDeLasPaginas()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.prod.yml")))
            carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);
        var programa = File.ReadAllText(Path.Combine(carpeta!.FullName, "src/Web/Backoffice/Program.cs"));

        var autorizacion = programa.IndexOf("app.UseAuthorization();", StringComparison.Ordinal);
        var middleware = programa.IndexOf("UseMiddleware<UltimaMilla.Backoffice.SesionDesplazadaMiddleware>", StringComparison.Ordinal);
        var paginas = programa.IndexOf("app.MapRazorPages();", StringComparison.Ordinal);

        Assert.True(autorizacion >= 0 && middleware > autorizacion && paginas > middleware, "SesionDesplazadaMiddleware debe registrarse entre UseAuthorization y MapRazorPages.");
    }

    [Fact]
    public async Task ElLogin_ExplicaPorQueSeCerroLaSesion()
    {
        var cliente = _fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var html = WebUtility.HtmlDecode(await (await cliente.GetAsync("/Login?motivo=sesion-desplazada")).Content.ReadAsStringAsync());

        Assert.Contains("Tu sesión se cerró porque se inició sesión con esta cuenta en otro dispositivo", html);
    }

    [Fact]
    public async Task ElLoginSinMotivo_NoMuestraElAviso()
    {
        var cliente = _fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var html = WebUtility.HtmlDecode(await (await cliente.GetAsync("/Login")).Content.ReadAsStringAsync());

        Assert.DoesNotContain("otro dispositivo", html);
    }
}
