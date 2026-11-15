using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UltimaMilla.Backoffice.Pages;
using UltimaMilla.Frontend.Infrastructure.Mocks;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// Al cerrar sesión y entrar con otro perfil, el login llevaba a la dirección que la sesión anterior había dejado (por ejemplo /Rutas/Index del despachador) y el
/// nuevo usuario, sin permiso para esa pantalla, veía "Acceso denegado". Ahora siempre se entra al inicio del perfil que acaba de iniciar sesión.
/// </summary>
public class LoginSinRetornoTests
{
    private sealed class AutenticacionFalsa : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private sealed class EntornoDeDesarrollo : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Pruebas";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Theory]
    [InlineData("Operario", "/Despacho/Index")]
    [InlineData("Despachador", "/Rutas/Index")]
    [InlineData("AdministradorPlataforma", "/Plataforma/Funcionalidades")]
    [InlineData("Administrador", "/Index")]
    public async Task ElLoginIgnoraLaDireccionDeRetornoDeLaSesionAnterior_YVaAlInicioDelPerfil(string perfil, string paginaEsperada)
    {
        var servicios = new ServiceCollection().AddSingleton<IAuthenticationService>(new AutenticacionFalsa()).BuildServiceProvider();
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:PermitirModoDemo"] = "true" }).Build();
        var modelo = new LoginModel(new MockContextoOperador(), configuracion, new EntornoDeDesarrollo())
        {
            RolSeleccionado = perfil,
            NombreUsuario = "Prueba",
            PageContext = new PageContext { HttpContext = new DefaultHttpContext { RequestServices = servicios } }
        };

        // La sesión anterior dejó una pantalla que este perfil no puede abrir
        var resultado = await modelo.OnPostAsync(returnUrl: "/Comercios/Index");

        var redireccion = Assert.IsType<RedirectToPageResult>(resultado);
        Assert.Equal(paginaEsperada, redireccion.PageName);
    }
}
