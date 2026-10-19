using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Rutas;

namespace UltimaMilla.Backoffice.Pages.Despacho;

/// <summary>
/// Modelo de página para el comprobante y resumen post-despacho de una hoja de ruta (UC-09).
/// </summary>
[Authorize(Roles = "Operario,Administrador")]
public class ComprobanteModel : PageModel
{
    private readonly IRutasApi _rutasApi;

    public ComprobanteModel(IRutasApi rutasApi)
    {
        _rutasApi = rutasApi;
    }

    public HojaDeRutaDto? HojaDeRuta { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancelacion)
    {
        HojaDeRuta = await _rutasApi.ObtenerHojaDeRutaPorIdAsync(id, cancelacion);
        if (HojaDeRuta == null)
        {
            TempData["MensajeError"] = "Hoja de ruta no encontrada.";
            return RedirectToPage("/Despacho/Index");
        }

        return Page();
    }
}
