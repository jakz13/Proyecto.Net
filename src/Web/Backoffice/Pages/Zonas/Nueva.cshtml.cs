using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.Zonas;

[Authorize(Roles = "Administrador")]
public class NuevaModel : PageModel
{
    private readonly IZonasCoberturaApi _zonasApi;

    public NuevaModel(IZonasCoberturaApi zonasApi)
    {
        _zonasApi = zonasApi;
    }

    [BindProperty]
    public CrearZonaCoberturaDto NuevaZona { get; set; } = new();

    [BindProperty]
    public List<CrearFranjaHorariaDto> Franjas { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
        // Valores iniciales por defecto para facilitar la carga al operador
        NuevaZona = new CrearZonaCoberturaDto
        {
            ColorHex = "#2563eb",
            PoligonoGeografico = "[[-34.9080, -56.2120], [-34.9030, -56.1780], [-34.9180, -56.1480], [-34.9310, -56.1600], [-34.9080, -56.2120]]"
        };

        // Franjas sugeridas iniciales
        Franjas = new List<CrearFranjaHorariaDto>
        {
            new() { DiaSemana = DayOfWeek.Monday, HoraInicio = new TimeOnly(9, 0), HoraFin = new TimeOnly(13, 0), CapacidadMaximaEnvios = 35 },
            new() { DiaSemana = DayOfWeek.Monday, HoraInicio = new TimeOnly(14, 0), HoraFin = new TimeOnly(18, 0), CapacidadMaximaEnvios = 40 }
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Franjas == null || Franjas.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Debe asociar al menos una franja horaria de entrega a la zona de cobertura.");
            return Page();
        }

        // Validación de superposiciones inconsistentes (Flujo principal de UC-02)
        var (esValido, mensajeError) = await _zonasApi.ValidarSuperposicionFranjasAsync(Franjas);
        if (!esValido)
        {
            ErrorMessage = mensajeError;
            ModelState.AddModelError(string.Empty, mensajeError ?? "Superposición horaria inconsistente detectada.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var zonaConFranjas = NuevaZona with { FranjasHorarias = Franjas };
            var zonaCreada = await _zonasApi.CrearZonaAsync(zonaConFranjas);

            TempData["MensajeExito"] = $"La zona de cobertura '{zonaCreada.Nombre}' y sus {zonaCreada.TotalFranjas} franjas horarias fueron guardadas exitosamente.";
            return RedirectToPage("/Zonas/Index");
        }
        catch (ApiExcepcion ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al registrar la zona: {ex.Message}";
            return Page();
        }
    }
}
