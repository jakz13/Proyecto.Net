using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.ReglasOperativas;

namespace UltimaMilla.Backoffice.Pages.ReglasOperativas;

[Authorize(Roles = "Administrador")]
public class DetalleModel : PageModel
{
    private readonly IReglasOperativasApi _reglasApi;

    public DetalleModel(IReglasOperativasApi reglasApi)
    {
        _reglasApi = reglasApi;
    }

    public ReglaOperativaDto Regla { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var entidad = await _reglasApi.ObtenerPorIdAsync(id);
        if (entidad == null)
        {
            return NotFound();
        }

        Regla = entidad;
        return Page();
    }
}
