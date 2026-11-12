using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Despacho;

namespace UltimaMilla.Backoffice.Pages.Despacho;

/// <summary>
/// Modelo de página para la verificación interactiva de carga y confirmación de salida de la hoja de ruta.
/// </summary>
[Authorize(Roles = "Despachador,Operario")]
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
        try
        {
            var operarioNombre = User.Identity?.Name ?? "Operario de depósito";
            var resultado = await _despachoApi.ConfirmarDespachoAsync(Formulario, operarioNombre, cancelacion);

            if (!resultado.Exitoso)
            {
                ErroresValidacion = resultado.Errores.Count > 0
                    ? resultado.Errores
                    : new List<string> { resultado.Mensaje };
                Control = await _despachoApi.ObtenerControlDespachoAsync(Formulario.HojaDeRutaId, cancelacion);
                return Page();
            }

            TempData["MensajeExito"] = resultado.Mensaje;
            return RedirectToPage("/Despacho/Comprobante", new { id = Formulario.HojaDeRutaId });
        }
        catch (Exception ex)
        {
            ErroresValidacion = new List<string> { ex.Message };
            Control = await _despachoApi.ObtenerControlDespachoAsync(Formulario.HojaDeRutaId, cancelacion);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRendirRutaPreviaAsync(string numeroRuta, CancellationToken cancelacion)
    {
        try
        {
            var exito = await _despachoApi.RendirRutaPreviaAsync(numeroRuta, cancelacion);
            if (exito)
            {
                TempData["MensajeExito"] = $"Rendición de la hoja de ruta previa '{numeroRuta}' registrada en depósito. El repartidor quedó habilitado para una nueva salida.";
            }
            else
            {
                TempData["MensajeError"] = $"No se pudo registrar automáticamente la rendición de la ruta previa '{numeroRuta}'. Verifique su estado en Rendición de Rutas.";
            }
        }
        catch (Exception ex)
        {
            TempData["MensajeError"] = ex.Message;
        }

        return RedirectToPage("/Despacho/Verificar", new { id = Formulario.HojaDeRutaId });
    }
}
