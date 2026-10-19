using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Rutas;

namespace UltimaMilla.Backoffice.Pages.Despacho;

/// <summary>
/// Modelo de página para el listado de hojas de ruta pendientes de control de salida y despacho físico (UC-09).
/// </summary>
[Authorize(Roles = "Despachador,Operario,Administrador")]
public class IndexModel : PageModel
{
    private readonly IDespachoApi _despachoApi;

    public IndexModel(IDespachoApi despachoApi)
    {
        _despachoApi = despachoApi;
    }

    public IReadOnlyList<HojaDeRutaDto> RutasPendientes { get; private set; } = new List<HojaDeRutaDto>();

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancelacion)
    {
        RutasPendientes = await _despachoApi.ListarRutasPendientesDespachoAsync(cancelacion);
        return Page();
    }
}
