using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.ReglasOperativas;

namespace UltimaMilla.Backoffice.Pages.ReglasOperativas;

/// <summary>
/// Vista principal del caso de uso UC-04: Configurar reglas operativas.
/// Permite gestionar la versión vigente, revisar el historial cerrado y simular el cumplimiento de reglas (RF-04).
/// </summary>
[Authorize(Roles = "Administrador")]
public class IndexModel : PageModel
{
    private readonly IReglasOperativasApi _reglasApi;

    public IndexModel(IReglasOperativasApi reglasApi)
    {
        _reglasApi = reglasApi;
    }

    public ReglaOperativaDto? VersionVigente { get; set; }
    public IReadOnlyList<ReglaOperativaDto> HistorialVersiones { get; set; } = Array.Empty<ReglaOperativaDto>();

    [BindProperty]
    public SimularReglaOperativaDto ParametrosSimulador { get; set; } = new();

    public ResultadoSimulacionReglasDto? ResultadoSimulacion { get; set; }

    public string? MensajeExito { get; set; }
    public string? MensajeError { get; set; }

    public async Task OnGetAsync()
    {
        MensajeExito = TempData["MensajeExito"]?.ToString();
        MensajeError = TempData["MensajeError"]?.ToString();

        await CargarDatosAsync();
    }

    public async Task<IActionResult> OnPostSimularReglasAsync()
    {
        await CargarDatosAsync();

        try
        {
            ResultadoSimulacion = await _reglasApi.SimularReglasAsync(ParametrosSimulador);
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al simular las reglas operativas: {ex.Message}";
        }

        return Page();
    }

    private async Task CargarDatosAsync()
    {
        var todasLasVersiones = await _reglasApi.ListarVersionesAsync();
        VersionVigente = todasLasVersiones.FirstOrDefault(v => v.EsVigente);
        HistorialVersiones = todasLasVersiones.Where(v => !v.EsVigente).ToList();
    }
}
