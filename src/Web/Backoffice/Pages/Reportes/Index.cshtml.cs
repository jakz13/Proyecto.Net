using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Reportes;

namespace UltimaMilla.Backoffice.Pages.Reportes;

/// <summary>
/// Controlador de la vista de reportes de gestión operativa y comercial (UC-13, RF-30).
/// </summary>
[Authorize(Roles = "Despachador,Administrador")]
public class IndexModel : PageModel
{
    private readonly IReportesApi _reportesApi;

    public IndexModel(IReportesApi reportesApi)
    {
        _reportesApi = reportesApi;
    }

    public ResultadoReporteDto Reporte { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public string TipoReporte { get; set; } = "CumplimientoZona";

    [BindProperty(SupportsGet = true)]
    public string Preajuste { get; set; } = "30Dias";

    [BindProperty(SupportsGet = true)]
    public string? FechaDesde { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FechaHasta { get; set; }

    public async Task OnGetAsync()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly desde;
        DateOnly hasta = hoy;

        switch (Preajuste)
        {
            case "Hoy":
                desde = hoy;
                break;
            case "7Dias":
                desde = hoy.AddDays(-7);
                break;
            case "EsteMes":
                desde = new DateOnly(hoy.Year, hoy.Month, 1);
                break;
            case "30Dias":
            default:
                desde = hoy.AddDays(-30);
                break;
        }

        // Si se especificaron fechas manuales, sobreescribir
        if (!string.IsNullOrEmpty(FechaDesde) && DateOnly.TryParse(FechaDesde, out var d))
        {
            desde = d;
        }
        else
        {
            FechaDesde = desde.ToString("yyyy-MM-dd");
        }

        if (!string.IsNullOrEmpty(FechaHasta) && DateOnly.TryParse(FechaHasta, out var h))
        {
            hasta = h;
        }
        else
        {
            FechaHasta = hasta.ToString("yyyy-MM-dd");
        }

        var filtro = new FiltroReporteDto
        {
            TipoReporte = TipoReporte,
            FechaDesde = desde,
            FechaHasta = hasta,
            Preajuste = Preajuste
        };

        Reporte = await _reportesApi.GenerarReporteAsync(filtro);
    }

    /// <summary>
    /// Exporta los datos consolidados del reporte en formato CSV compatible con Excel.
    /// </summary>
    public async Task<IActionResult> OnGetExportarCsvAsync()
    {
        await OnGetAsync();
        var filtro = Reporte.Filtro;
        var bytes = await _reportesApi.ExportarReporteCsvAsync(filtro);
        var nombreArchivo = $"Reporte_{filtro.TipoReporte}_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv; charset=utf-8", nombreArchivo);
    }
}
