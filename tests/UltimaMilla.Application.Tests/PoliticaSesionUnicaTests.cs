using System.Security.Claims;
using UltimaMilla.Application.Common;
using Xunit;

namespace UltimaMilla.Application.Tests;

/// <summary>
/// La sesión única activa (un nuevo inicio de sesión deja sin efecto los tokens anteriores) aplica a todos los perfiles: una cuenta no puede usarse
/// a la vez desde dos sesiones (Backoffice, Portal del comercio o app móvil).
/// </summary>
public class PoliticaSesionUnicaTests
{
    [Theory]
    [InlineData("Repartidor", true)]
    [InlineData("repartidor", true)]
    [InlineData(" Repartidor ", true)]
    [InlineData("Administrador", true)]
    [InlineData("AdministradorPlataforma", true)]
    [InlineData("Despachador", true)]
    [InlineData("Operario", true)]
    [InlineData("Comercio", true)]
    [InlineData("SinPerfil", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void PorPerfil_AplicaATodosLosPerfilesConNombre(string? perfil, bool aplica)
        => Assert.Equal(aplica, PoliticaSesionUnica.AplicaAlPerfil(perfil));

    private static ClaimsPrincipal Usuario(string? rol, bool autenticado = true, string tipoClaim = ClaimTypes.Role)
    {
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()) };
        if (rol is not null)
            claims.Add(new Claim(tipoClaim, rol));
        return new ClaimsPrincipal(autenticado ? new ClaimsIdentity(claims, "Bearer") : new ClaimsIdentity(claims));
    }

    [Theory]
    [InlineData("Repartidor", true)]
    [InlineData("Administrador", true)]
    [InlineData("Despachador", true)]
    [InlineData("Operario", true)]
    [InlineData("Comercio", true)]
    public void PorIdentidad_AplicaATodosLosPerfilesAutenticados(string rol, bool aplica)
    {
        Assert.Equal(aplica, PoliticaSesionUnica.AplicaA(Usuario(rol)));
        Assert.Equal(aplica, PoliticaSesionUnica.AplicaA(Usuario(rol, tipoClaim: "role")));
    }

    [Fact]
    public void SinSesionOSinRol_NoAplica()
    {
        Assert.False(PoliticaSesionUnica.AplicaA(null));
        Assert.False(PoliticaSesionUnica.AplicaA(Usuario(null)));
        Assert.False(PoliticaSesionUnica.AplicaA(Usuario("Repartidor", autenticado: false)));
    }
}
