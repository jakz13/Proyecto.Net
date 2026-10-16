using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.IdentidadVisual;

/// <summary>
/// Modelo de página para el caso de uso UC-06: Gestionar identidad visual del operador.
/// Permite al Administrador configurar logotipo, paleta cromática y canales de contacto oficial,
/// reflejando la marca en el portal del comercio, seguimiento público y comunicaciones (RF-05).
/// </summary>
[Authorize(Roles = "Administrador")]
public class IndexModel : PageModel
{
    private readonly IIdentidadVisualApi _identidadApi;

    public IndexModel(IIdentidadVisualApi identidadApi)
    {
        _identidadApi = identidadApi;
    }

    public IdentidadVisualDto IdentidadActual { get; set; } = new();

    [BindProperty]
    public ActualizarIdentidadVisualDto Modelo { get; set; } = new();

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    public async Task OnGetAsync()
    {
        await CargarIdentidadAsync();

        // Inicializar el formulario con los valores actuales
        Modelo = new ActualizarIdentidadVisualDto
        {
            NombreComercial = IdentidadActual.NombreComercial,
            ColorPrimario = IdentidadActual.ColorPrimario,
            ColorSecundario = IdentidadActual.ColorSecundario,
            LogoUrl = IdentidadActual.LogoUrl,
            EmailContacto = IdentidadActual.EmailContacto ?? "contacto@operador.uy",
            TelefonoContacto = IdentidadActual.TelefonoContacto ?? "+598 2900 0000",
            SitioWeb = IdentidadActual.SitioWeb ?? "https://translogistica.uy",
            MensajeComunicaciones = IdentidadActual.MensajeComunicaciones ?? $"Gracias por elegir {IdentidadActual.NombreComercial}. Tu paquete está en camino."
        };
    }

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        if (!ModelState.IsValid)
        {
            MensajeError = "Por favor verifica que todos los campos obligatorios cumplan con los formatos requeridos.";
            await CargarIdentidadAsync();
            return Page();
        }

        try
        {
            var actualizada = await _identidadApi.ActualizarIdentidadVisualAsync(Modelo);
            MensajeExito = $"Identidad visual de '{actualizada.NombreComercial}' guardada exitosamente. Se aplicó en el portal del comercio, seguimiento público y comunicaciones (RF-05).";
            return RedirectToPage();
        }
        catch (ApiExcepcion ex)
        {
            MensajeError = ex.Message;
            await CargarIdentidadAsync();
            return Page();
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al actualizar la identidad visual: {ex.Message}";
            await CargarIdentidadAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRestablecerPredeterminadosAsync()
    {
        try
        {
            var valoresPorDefecto = new ActualizarIdentidadVisualDto
            {
                NombreComercial = "TransLogística Express",
                ColorPrimario = "#1D4ED8",
                ColorSecundario = "#F97316",
                LogoUrl = null,
                EmailContacto = "contacto@translogistica.uy",
                TelefonoContacto = "+598 2900 1122",
                SitioWeb = "https://translogistica.uy",
                MensajeComunicaciones = "Gracias por confiar en TransLogística Express. Tu paquete está en camino."
            };

            await _identidadApi.ActualizarIdentidadVisualAsync(valoresPorDefecto);
            MensajeExito = "Identidad visual y paleta de colores restablecidas a los valores de marca iniciales.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al restablecer valores: {ex.Message}";
            await CargarIdentidadAsync();
            return Page();
        }
    }

    private async Task CargarIdentidadAsync()
    {
        IdentidadActual = await _identidadApi.ObtenerIdentidadActualAsync();
    }
}
