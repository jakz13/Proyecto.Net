using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Comercios;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.Comercios;

[Authorize(Roles = "Administrador")]
public class EditarModel : PageModel
{
    private readonly IComerciosApi _comerciosApi;

    public EditarModel(IComerciosApi comerciosApi)
    {
        _comerciosApi = comerciosApi;
    }

    [BindProperty]
    public Guid Id { get; set; }

    [BindProperty]
    public ActualizarComercioDto Comercio { get; set; } = new();

    public string? RutOriginal { get; set; }
    public string? ErrorConcurrencia { get; set; }
    public string? ErrorGeneral { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Id = id;
        var entidad = await _comerciosApi.ObtenerPorIdAsync(id);
        if (entidad == null)
        {
            return NotFound();
        }

        RutOriginal = entidad.Rut;
        Comercio = new ActualizarComercioDto
        {
            RazonSocial = entidad.RazonSocial,
            NombreFantasia = entidad.NombreFantasia,
            Email = entidad.Email,
            Telefono = entidad.Telefono,
            Direccion = entidad.Direccion,
            Activo = entidad.Activo,
            SelloModificacion = entidad.SelloModificacion
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _comerciosApi.ActualizarComercioAsync(Id, Comercio);
            TempData["MensajeExito"] = $"El comercio '{Comercio.NombreFantasia}' fue actualizado correctamente.";
            return RedirectToPage("/Comercios/Index");
        }
        catch (ApiExcepcion ex) when (ex.EsConflictoConcurrencia)
        {
            // Regla obligatoria 7: ante 409 mostrar "otra persona modificó este registro, recargá y reintentá"
            ErrorConcurrencia = "otra persona modificó este registro, recargá y reintentá.";
            return Page();
        }
        catch (Exception ex)
        {
            ErrorGeneral = $"Error inesperado: {ex.Message}";
            return Page();
        }
    }

    // Endpoint POST de prueba para la demo oral: simula enviar un sello alterado para gatillar 409 Conflict
    public IActionResult OnPostSimularConflicto()
    {
        Comercio.SelloModificacion = "sello-invalido-para-forzar-conflicto";
        return OnPostAsync().Result;
    }
}
