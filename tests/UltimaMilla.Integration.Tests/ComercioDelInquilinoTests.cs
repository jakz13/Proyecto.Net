using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UltimaMilla.Infrastructure.Services;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// El comercio del inquilino solo existe para usuarios de comercio. El personal de un operador (despachador, operario…)
/// no tiene comercio y debe ver los envíos de todos los comercios de su operador; antes recibía el comercio demo
/// y el listado de "Gestión de envíos" le salía vacío en cualquier operador distinto del demo.
/// </summary>
public class ComercioDelInquilinoTests
{
    private static CurrentTenant ConUsuario(params Claim[] claims)
    {
        var contexto = new DefaultHttpContext();
        if (claims.Length > 0)
            contexto.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        return new CurrentTenant(new HttpContextAccessor { HttpContext = contexto });
    }

    [Fact]
    public void Personal_del_operador_autenticado_sin_comercio_no_recibe_el_comercio_demo()
    {
        var tenant = ConUsuario(new Claim("operador_id", Guid.NewGuid().ToString()), new Claim("role", "Despachador"));

        Assert.Null(tenant.ComercioOperadorId);
    }

    [Fact]
    public void Usuario_de_comercio_conserva_su_comercio()
    {
        var comercio = Guid.NewGuid();
        var tenant = ConUsuario(new Claim("operador_id", Guid.NewGuid().ToString()), new Claim("comercio_operador_id", comercio.ToString()));

        Assert.Equal(comercio, tenant.ComercioOperadorId);
    }

    [Fact]
    public void Sin_sesion_se_mantiene_el_comercio_demo()
    {
        var tenant = ConUsuario();

        Assert.NotNull(tenant.ComercioOperadorId);
    }
}
