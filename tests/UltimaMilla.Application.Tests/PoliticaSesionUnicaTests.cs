using System.Security.Claims;
using UltimaMilla.Application.Common;
using Xunit;

namespace UltimaMilla.Application.Tests;

/// <summary>
/// La sesión única activa (un nuevo inicio de sesión deja sin efecto los tokens anteriores) aplica SOLO a repartidores. Antes se aplicaba a todas las cuentas y, con
/// cuentas compartidas (demostración, dos navegadores en el Backoffice), las personas se expulsaban entre sí.
/// </summary>
public class PoliticaSesionUnicaTests
{
    [Theory]
    [InlineData("Repartidor", true)]
    [InlineData("repartidor", true)]
    [InlineData(" Repartidor ", true)]
    [InlineData("Administrador", false)]
    [InlineData("AdministradorPlataforma", false)]
    [InlineData("Despachador", false)]
    [InlineData("Operario", false)]
    [InlineData("Comercio", false)]
    [InlineData("SinPerfil", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void PorPerfil_SoloAplicaAlRepartidor(string? perfil, bool aplica)
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
    [InlineData("Administrador", false)]
    [InlineData("Despachador", false)]
    [InlineData("Operario", false)]
    [InlineData("Comercio", false)]
    public void PorIdentidad_SoloAplicaAlRepartidorAutenticado(string rol, bool aplica)
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
