using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Tarifas;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;

namespace UltimaMilla.Backoffice.Pages.Tarifas;

/// <summary>
/// Vista principal del caso de uso UC-03: Configurar el cuadro tarifario.
/// Permite gestionar la versión vigente, revisar el historial cerrado y simular cotizaciones (RF-03).
/// </summary>
[Authorize(Roles = "Administrador")]
public class IndexModel : PageModel
{
    private readonly ICuadroTarifarioApi _tarifasApi;
    private readonly IZonasCoberturaApi _zonasApi;

    public IndexModel(ICuadroTarifarioApi tarifasApi, IZonasCoberturaApi zonasApi)
    {
        _tarifasApi = tarifasApi;
        _zonasApi = zonasApi;
    }

    public CuadroTarifarioDto? VersionVigente { get; set; }
    public IReadOnlyList<CuadroTarifarioDto> HistorialVersiones { get; set; } = Array.Empty<CuadroTarifarioDto>();
    public IReadOnlyList<ZonaCoberturaDto> Zonas { get; set; } = Array.Empty<ZonaCoberturaDto>();

    [BindProperty]
    public SimularTarifaDto ParametrosSimulador { get; set; } = new();

    public CotizacionSimuladaDto? ResultadoCotizacion { get; set; }

    public string? MensajeExito { get; set; }
    public string? MensajeError { get; set; }

    public async Task OnGetAsync()
    {
        MensajeExito = TempData["MensajeExito"]?.ToString();
        MensajeError = TempData["MensajeError"]?.ToString();

        await CargarDatosAsync();
    }

    public async Task<IActionResult> OnPostSimularCotizacionAsync()
    {
        await CargarDatosAsync();

        try
        {
            ResultadoCotizacion = await _tarifasApi.SimularTarifaAsync(ParametrosSimulador);
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al simular cotización: {ex.Message}";
        }

        return Page();
    }

    private async Task CargarDatosAsync()
    {
        var todasLasVersiones = await _tarifasApi.ListarVersionesAsync();
        VersionVigente = todasLasVersiones.FirstOrDefault(v => v.EsVigente);
        HistorialVersiones = todasLasVersiones.Where(v => !v.EsVigente).ToList();
        Zonas = await _zonasApi.ListarZonasAsync(new FiltroZonasDto { SoloActivas = true });
    }
}
