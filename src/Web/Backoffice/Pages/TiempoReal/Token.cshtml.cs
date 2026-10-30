using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Backoffice.Seguridad;
using UltimaMilla.Frontend.Infrastructure.Mocks;

namespace UltimaMilla.Backoffice.Pages.TiempoReal;

/// <summary>
/// Token para las conexiones de tiempo real de cualquier página del backoffice (estado de los envíos en vivo).
/// Exige sesión iniciada; el operador del token es el de la sesión.
/// </summary>
[Authorize]
public class TokenModel : PageModel
{
    private readonly IConfiguration? _configuracion;
    private readonly MockContextoOperador? _contextoOperador;

    public TokenModel(IConfiguration? configuracion = null, MockContextoOperador? contextoOperador = null)
    {
        _configuracion = configuracion;
        _contextoOperador = contextoOperador;
    }

    public IActionResult OnGet()
    {
        Response.Headers.CacheControl = "no-store";
        return new JsonResult(EmisorTokenTiempoReal.Emitir(User, _configuracion, _contextoOperador, "/hubs/tablero"));
    }
}
