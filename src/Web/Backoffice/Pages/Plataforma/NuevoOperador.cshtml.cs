using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Funcionalidades;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.Plataforma;

/// <summary>
/// Alta de una empresa de envíos por el administrador de la plataforma. La empresa nace con un administrador inicial obligatorio:
/// con esa cuenta entra al Backoffice y crea los demás perfiles (despachadores, operarios, repartidores, comercios).
/// </summary>
[Authorize(Roles = FuncionalidadesModel.RolPlataforma)]
public class NuevoOperadorModel : PageModel
{
    private readonly IFuncionalidadesApi _api;

    public NuevoOperadorModel(IFuncionalidadesApi api) => _api = api;

    [BindProperty]
    public CrearOperadorDto Operador { get; set; } = new();

    public string? MensajeError { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            await _api.CrearOperadorAsync(Operador);
            TempData["MensajeEmpresaCreada"] = $"La empresa '{Operador.NombreComercial}' fue creada. Su administrador ({Operador.AdministradorEmail}) ya puede ingresar al Backoffice.";
            return RedirectToPage("/Plataforma/Funcionalidades");
        }
        catch (ApiExcepcion ex)
        {
            MensajeError = ex.Message;
            return Page();
        }
    }
}
