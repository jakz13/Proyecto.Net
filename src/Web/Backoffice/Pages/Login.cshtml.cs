using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Infrastructure.Mocks;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Backoffice.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly MockContextoOperador _contextoOperador;

    public LoginModel(MockContextoOperador contextoOperador)
    {
        _contextoOperador = contextoOperador;
    }

    [BindProperty]
    public string RolSeleccionado { get; set; } = "Administrador";

    [BindProperty]
    public string NombreUsuario { get; set; } = "Felipe Bernardi";

    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        // En esta etapa de demo/desarrollo, autenticamos con el rol elegido emitiendo la Cookie con Claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, NombreUsuario),
            new(ClaimTypes.Role, RolSeleccionado),
            new("operador_id", _contextoOperador.OperadorActivo.OperadorId),
            new("tenant_id", ResolverOperadorGuid(_contextoOperador.OperadorActivo.OperadorId).ToString())
        };

        var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identidad);

        var propiedades = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, propiedades);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        if (RolSeleccionado == "Operario")
        {
            return RedirectToPage("/Despacho/Index");
        }
        else if (RolSeleccionado == "Despachador")
        {
            return RedirectToPage("/Rutas/Index");
        }

        return RedirectToPage("/Index");
    }

    internal static Guid ResolverOperadorGuid(string? codigoOperador)
    {
        if (!string.IsNullOrWhiteSpace(codigoOperador) && Guid.TryParse(codigoOperador, out var guid) && guid != Guid.Empty)
            return guid;

        return string.Equals(codigoOperador, "OP-02", StringComparison.OrdinalIgnoreCase)
            ? DatabaseSeederSegundoOperador.OperadorId
            : DatabaseSeeder.DefaultOperadorId;
    }
}
