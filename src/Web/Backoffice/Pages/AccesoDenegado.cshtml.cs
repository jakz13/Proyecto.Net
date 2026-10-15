using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UltimaMilla.Backoffice.Pages;

[AllowAnonymous]
public class AccesoDenegadoModel : PageModel
{
    public void OnGet()
    {
    }
}
