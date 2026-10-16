using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Despacho;

namespace UltimaMilla.Backoffice.Pages.Despacho;

/// <summary>
/// Modelo de página para la verificación interactiva de carga y confirmación de salida de la hoja de ruta (UC-09).
/// </summary>
[Authorize(Roles = "Operario,Administrador")]
public class VerificarModel : PageModel
{
    private readonly IDespachoApi _despachoApi;

    public VerificarModel(IDespachoApi despachoApi)
    {
        _despachoApi = despachoApi;
    }

    public VerificacionDespachoDto? Control { get; private set; }

    [BindProperty]
    public ConfirmarDespachoDto Formulario { get; set; } = new();

    public List<string> ErroresValidacion { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancelacion)
    {
        Control = await _despachoApi.ObtenerControlDespachoAsync(id, cancelacion);
        if (Control == null)
        {
            TempData["MensajeError"] = "La hoja de ruta especificada no existe o no se encuentra disponible para despacho.";
            return RedirectToPage("/Despacho/Index");
        }

        if (Control.EstadoRuta == "Despachada" || Control.EstadoRuta == "EnTransito")
        {
            TempData["MensajeError"] = $"La hoja de ruta '{Control.NumeroRuta}' ya fue despachada previamente.";
            return RedirectToPage("/Despacho/Index");
        }

        Formulario = new ConfirmarDespachoDto
        {
            HojaDeRutaId = id
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancelacion)
    {
        var operarioNombre = User.Identity?.Name ?? "Operario de depósito";
        var resultado = await _despachoApi.ConfirmarDespachoAsync(Formulario, operarioNombre, cancelacion);

        if (!resultado.Exitoso)
        {
            ErroresValidacion = resultado.Errores;
            Control = await _despachoApi.ObtenerControlDespachoAsync(Formulario.HojaDeRutaId, cancelacion);
            return Page();
        }

        TempData["MensajeExito"] = resultado.Mensaje;
        return RedirectToPage("/Despacho/Comprobante", new { id = Formulario.HojaDeRutaId });
    }

    public async Task<IActionResult> OnPostRendirRutaPreviaAsync(string numeroRuta, CancellationToken cancelacion)
    {
        await _despachoApi.RendirRutaPreviaAsync(numeroRuta, cancelacion);
        TempData["MensajeExito"] = $"Rendición de la hoja de ruta previa '{numeroRuta}' registrada en depósito. El repartidor quedó habilitado para una nueva salida.";
        return RedirectToPage("/Despacho/Verificar", new { id = Formulario.HojaDeRutaId });
    }
}
