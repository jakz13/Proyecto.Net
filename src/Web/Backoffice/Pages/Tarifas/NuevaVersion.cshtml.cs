using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Tarifas;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.Tarifas;

[Authorize(Roles = "Administrador")]
public class NuevaVersionModel : PageModel
{
    private readonly ICuadroTarifarioApi _tarifasApi;
    private readonly IZonasCoberturaApi _zonasApi;

    public NuevaVersionModel(ICuadroTarifarioApi tarifasApi, IZonasCoberturaApi zonasApi)
    {
        _tarifasApi = tarifasApi;
        _zonasApi = zonasApi;
    }

    [BindProperty]
    public CrearVersionCuadroTarifarioDto NuevaVersion { get; set; } = new();

    [BindProperty]
    public List<CrearReglaTarifaDto> Reglas { get; set; } = new();

    public CuadroTarifarioDto? VersionActual { get; set; }
    public IReadOnlyList<ZonaCoberturaDto> Zonas { get; set; } = Array.Empty<ZonaCoberturaDto>();

    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        VersionActual = await _tarifasApi.ObtenerVersionVigenteAsync();
        Zonas = await _zonasApi.ListarZonasAsync(new FiltroZonasDto { SoloActivas = true });

        if (VersionActual != null && VersionActual.ReglasTarifa.Count > 0)
        {
            // Pre-poblar con las reglas de la versión actual para facilitar modificaciones incrementales
            Reglas = VersionActual.ReglasTarifa.Select(r => new CrearReglaTarifaDto
            {
                ZonaCoberturaId = r.ZonaCoberturaId,
                ModalidadServicio = r.ModalidadServicio,
                PesoDesde = r.PesoDesde,
                PesoHasta = r.PesoHasta,
                VolumenDesde = r.VolumenDesde,
                VolumenHasta = r.VolumenHasta,
                PrecioBase = r.PrecioBase,
                Recargo = r.Recargo,
                Bonificacion = r.Bonificacion
            }).ToList();

            NuevaVersion = new CrearVersionCuadroTarifarioDto
            {
                VigenteDesde = DateTime.UtcNow,
                Comentario = $"Actualización de precios sobre Versión {VersionActual.Version}"
            };
        }
        else
        {
            // Reglas base sugeridas
            Reglas = new List<CrearReglaTarifaDto>
            {
                new() { ModalidadServicio = "Estándar 24-48h", PesoDesde = 0, PesoHasta = 5, VolumenDesde = 0, VolumenHasta = 0.05m, PrecioBase = 180 },
                new() { ModalidadServicio = "Express en el Día", PesoDesde = 0, PesoHasta = 5, VolumenDesde = 0, VolumenHasta = 0.05m, PrecioBase = 250, Recargo = 50 }
            };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Zonas = await _zonasApi.ListarZonasAsync(new FiltroZonasDto { SoloActivas = true });
        VersionActual = await _tarifasApi.ObtenerVersionVigenteAsync();

        if (Reglas == null || Reglas.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Debe configurar al menos una regla de precio para la nueva versión del cuadro tarifario.");
            return Page();
        }

        // Validación de rangos de cada regla
        for (int i = 0; i < Reglas.Count; i++)
        {
            var regla = Reglas[i];
            if (regla.PesoDesde >= regla.PesoHasta)
            {
                ModelState.AddModelError(string.Empty, $"En la regla #{i + 1} ({regla.ModalidadServicio}), el peso inicial debe ser menor al peso final.");
                return Page();
            }
            if (regla.VolumenDesde >= regla.VolumenHasta)
            {
                ModelState.AddModelError(string.Empty, $"En la regla #{i + 1} ({regla.ModalidadServicio}), el volumen inicial debe ser menor al volumen final.");
                return Page();
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var versionCreada = await _tarifasApi.PublicarNuevaVersionAsync(NuevaVersion with { ReglasTarifa = Reglas });

            TempData["MensajeExito"] = $"La Versión {versionCreada.Version} del cuadro tarifario fue publicada exitosamente como vigente. La versión anterior quedó archivada con su rango de vigencia cerrado.";
            return RedirectToPage("/Tarifas/Index");
        }
        catch (ApiExcepcion ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al publicar la nueva versión: {ex.Message}";
            return Page();
        }
    }
}
