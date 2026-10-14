using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.ReglasOperativas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.ReglasOperativas;

[Authorize(Roles = "Administrador")]
public class NuevaVersionModel : PageModel
{
    private readonly IReglasOperativasApi _reglasApi;

    public NuevaVersionModel(IReglasOperativasApi reglasApi)
    {
        _reglasApi = reglasApi;
    }

    [BindProperty]
    public CrearReglaOperativaDto NuevaVersion { get; set; } = new();

    public ReglaOperativaDto? VersionActual { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        VersionActual = await _reglasApi.ObtenerVersionVigenteAsync();

        if (VersionActual != null)
        {
            // Pre-poblar los valores con la versión vigente para facilitar modificaciones operativas
            NuevaVersion = new CrearReglaOperativaDto
            {
                MaxIntentosEntrega = VersionActual.MaxIntentosEntrega,
                HorasEntreIntentos = VersionActual.HorasEntreIntentos,
                TipoPruebaEntregaExigida = VersionActual.TipoPruebaEntregaExigida,
                PoliticaDevolucion = VersionActual.PoliticaDevolucion,
                HorasPlazoComprometidoEstandar = VersionActual.HorasPlazoComprometidoEstandar,
                HorasPlazoComprometidoUrgente = VersionActual.HorasPlazoComprometidoUrgente,
                VigenteDesde = DateTime.UtcNow,
                Comentario = $"Actualización de reglas operativas sobre Versión {VersionActual.Version}"
            };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        VersionActual = await _reglasApi.ObtenerVersionVigenteAsync();

        if (NuevaVersion.HorasPlazoComprometidoUrgente > NuevaVersion.HorasPlazoComprometidoEstandar)
        {
            ModelState.AddModelError(
                nameof(NuevaVersion.HorasPlazoComprometidoUrgente),
                "El plazo comprometido para envíos urgentes no puede ser mayor al plazo estándar.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var versionCreada = await _reglasApi.PublicarNuevaVersionAsync(NuevaVersion);

            TempData["MensajeExito"] = $"La Versión {versionCreada.Version} de reglas operativas fue publicada exitosamente como vigente. La versión anterior quedó archivada con su rango de vigencia cerrado.";
            return RedirectToPage("/ReglasOperativas/Index");
        }
        catch (ApiExcepcion ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al publicar la nueva versión de reglas: {ex.Message}";
            return Page();
        }
    }
}
