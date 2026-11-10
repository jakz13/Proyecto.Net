using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.IdentidadVisual;

/// <summary>
/// Modelo de página para el caso de uso UC-06: Gestionar identidad visual del operador.
/// Permite al Administrador configurar logotipo (URL o carga de archivo local), paleta cromática
/// y canales de contacto oficial, reflejando la marca en todos los portales (RF-05).
/// </summary>
[Authorize(Roles = "Administrador")]
public class IndexModel : PageModel
{
    private readonly IIdentidadVisualApi _identidadApi;
    private readonly IWebHostEnvironment _entorno;

    public IndexModel(IIdentidadVisualApi identidadApi, IWebHostEnvironment entorno)
    {
        _identidadApi = identidadApi;
        _entorno = entorno;
    }

    public IdentidadVisualDto IdentidadActual { get; set; } = new();

    [BindProperty]
    public ActualizarIdentidadVisualDto Modelo { get; set; } = new();

    /// <summary>
    /// Archivo de imagen subido directamente desde la computadora del usuario.
    /// </summary>
    [BindProperty]
    public IFormFile? ArchivoLogo { get; set; }

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            await CargarIdentidadAsync();
        }
        catch (Exception ex)
        {
            // Resiliencia ante recargas forzadas o desconexión temporal del backend: mantiene valores seguros
            IdentidadActual = new IdentidadVisualDto
            {
                NombreComercial = "TransLogística Express",
                ColorPrimario = "#1D4ED8",
                ColorSecundario = "#F97316",
                EmailContacto = "contacto@translogistica.uy",
                TelefonoContacto = "+598 2900 1122"
            };
            MensajeError = $"No se pudo sincronizar la identidad en línea ({ex.Message}). Mostrando valores de respaldo.";
        }

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

    /// <summary>Comprueba los primeros bytes del archivo (firma PNG, JPEG o RIFF/WEBP) para no confiar solo en el nombre.</summary>
    private static async Task<bool> ContenidoCoincideConLaExtensionAsync(IFormFile archivo, string extension)
    {
        var cabecera = new byte[12];
        await using var flujo = archivo.OpenReadStream();
        var leidos = await flujo.ReadAsync(cabecera.AsMemory(0, cabecera.Length));
        if (leidos < 12)
            return false;

        return extension switch
        {
            ".png" => cabecera[0] == 0x89 && cabecera[1] == 0x50 && cabecera[2] == 0x4E && cabecera[3] == 0x47,
            ".jpg" or ".jpeg" => cabecera[0] == 0xFF && cabecera[1] == 0xD8 && cabecera[2] == 0xFF,
            ".webp" => cabecera[0] == 'R' && cabecera[1] == 'I' && cabecera[2] == 'F' && cabecera[3] == 'F'
                       && cabecera[8] == 'W' && cabecera[9] == 'E' && cabecera[10] == 'B' && cabecera[11] == 'P',
            _ => false
        };
    }

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        // Si el usuario subió una imagen desde su equipo, procesarla y guardarla en wwwroot/uploads/logos
        if (ArchivoLogo is not null && ArchivoLogo.Length > 0)
        {
            try
            {
                var rutaWebRoot = _entorno.WebRootPath ?? Path.Combine(_entorno.ContentRootPath, "wwwroot");
                var carpetaUploads = Path.Combine(rutaWebRoot, "uploads", "logos");
                Directory.CreateDirectory(carpetaUploads);

                // Solo imágenes rasterizadas, de hasta 2 MB y cuyo contenido coincida con la extensión (sin .svg ni .html, que ejecutan scripts)
                var extension = Path.GetExtension(ArchivoLogo.FileName).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".png";
                }

                if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp") || ArchivoLogo.Length > 2 * 1024 * 1024
                    || !await ContenidoCoincideConLaExtensionAsync(ArchivoLogo, extension))
                {
                    MensajeError = "El logo debe ser una imagen PNG, JPG o WEBP de hasta 2 MB.";
                    await CargarIdentidadAsync();
                    return Page();
                }

                var nombreArchivo = $"logo_empresa_{Guid.NewGuid():N}{extension}";
                var rutaDestino = Path.Combine(carpetaUploads, nombreArchivo);

                using (var stream = new FileStream(rutaDestino, FileMode.Create))
                {
                    await ArchivoLogo.CopyToAsync(stream);
                }

                // En entorno local de desarrollo, replica el logo en los otros portales para que se visualice sin errores 404
                try
                {
                    var rutasOtrosPortales = new[]
                    {
                        Path.Combine(_entorno.ContentRootPath, "..", "PortalComercio", "wwwroot", "uploads", "logos"),
                        Path.Combine(_entorno.ContentRootPath, "..", "SeguimientoPublico", "wwwroot", "uploads", "logos")
                    };

                    foreach (var carpetaDestino in rutasOtrosPortales)
                    {
                        if (Directory.Exists(Path.GetDirectoryName(carpetaDestino)))
                        {
                            Directory.CreateDirectory(carpetaDestino);
                            System.IO.File.Copy(rutaDestino, Path.Combine(carpetaDestino, nombreArchivo), overwrite: true);
                        }
                    }
                }
                catch
                {
                    // La réplica local es oportunista; no debe bloquear si los otros portales están ausentes
                }

                Modelo.LogoUrl = $"/uploads/logos/{nombreArchivo}";
            }
            catch (Exception ex)
            {
                MensajeError = "No se pudo guardar la imagen subida.";
                await CargarIdentidadAsync();
                return Page();
            }
        }

        if (!ModelState.IsValid)
        {
            MensajeError = "Por favor verifica que todos los campos obligatorios cumplan con los formatos requeridos.";
            await CargarIdentidadAsync();
            return Page();
        }

        try
        {
            var actualizada = await _identidadApi.ActualizarIdentidadVisualAsync(Modelo);
            MensajeExito = $"Identidad visual de '{actualizada.NombreComercial}' guardada exitosamente. Se aplicó en el panel lateral, portal del comercio, seguimiento público y comunicaciones (RF-05).";
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
