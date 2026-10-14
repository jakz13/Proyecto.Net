using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Tarifas;

namespace UltimaMilla.Backoffice.Pages.Tarifas;

[Authorize(Roles = "Administrador")]
public class DetalleModel : PageModel
{
    private readonly ICuadroTarifarioApi _tarifasApi;

    public DetalleModel(ICuadroTarifarioApi tarifasApi)
    {
        _tarifasApi = tarifasApi;
    }

    public CuadroTarifarioDto Cuadro { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var entidad = await _tarifasApi.ObtenerPorIdAsync(id);
        if (entidad == null)
        {
            return NotFound();
        }

        Cuadro = entidad;
        return Page();
    }
}
