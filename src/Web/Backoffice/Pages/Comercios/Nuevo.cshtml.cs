using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Comercios;

namespace UltimaMilla.Backoffice.Pages.Comercios;

[Authorize(Roles = "Administrador")]
public class NuevoModel : PageModel
{
    private readonly IComerciosApi _comerciosApi;

    public NuevoModel(IComerciosApi comerciosApi)
    {
        _comerciosApi = comerciosApi;
    }

    [BindProperty]
    public CrearComercioDto NuevoComercio { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _comerciosApi.CrearComercioAsync(NuevoComercio);
            TempData["MensajeExito"] = $"El comercio '{NuevoComercio.NombreFantasia}' fue registrado exitosamente.";
            return RedirectToPage("/Comercios/Index");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al registrar el comercio: {ex.Message}";
            return Page();
        }
    }
}
