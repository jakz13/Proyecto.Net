using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.Zonas;

[Authorize(Roles = "Administrador")]
public class EditarModel : PageModel
{
    private readonly IZonasCoberturaApi _zonasApi;

    public EditarModel(IZonasCoberturaApi zonasApi)
    {
        _zonasApi = zonasApi;
    }

    [BindProperty]
    public Guid Id { get; set; }

    [BindProperty]
    public ActualizarZonaCoberturaDto Zona { get; set; } = new();

    [BindProperty]
    public List<CrearFranjaHorariaDto> Franjas { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Id = id;
        var entidad = await _zonasApi.ObtenerPorIdAsync(id);
        if (entidad == null)
        {
            return NotFound();
        }

        Zona = new ActualizarZonaCoberturaDto
        {
            Nombre = entidad.Nombre,
            PoligonoGeografico = entidad.PoligonoGeografico,
            ColorHex = entidad.ColorHex,
            Activa = entidad.Activa
        };

        Franjas = entidad.FranjasHorarias.Select(f => new CrearFranjaHorariaDto
        {
            DiaSemana = f.DiaSemana,
            HoraInicio = f.HoraInicio,
            HoraFin = f.HoraFin,
            CapacidadMaximaEnvios = f.CapacidadMaximaEnvios
        }).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Franjas == null || Franjas.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Debe conservar al menos una franja horaria de entrega en la zona de cobertura.");
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
            var zonaActualizada = Zona with { FranjasHorarias = Franjas };
            await _zonasApi.ActualizarZonaAsync(Id, zonaActualizada);

            TempData["MensajeExito"] = $"La zona de cobertura '{Zona.Nombre}' y sus {Franjas.Count} franjas horarias fueron actualizadas exitosamente.";
            return RedirectToPage("/Zonas/Index");
        }
        catch (ApiExcepcion ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al actualizar la zona: {ex.Message}";
            return Page();
        }
    }
}
