using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;

namespace UltimaMilla.Backoffice.Pages.Zonas;

/// <summary>
/// Vista principal del caso de uso Configurar zonas de cobertura y franjas horarias.
/// Permite visualizar polígonos, filtrar, ver horarios de entrega y alternar estado activo/inactivo.
/// </summary>
[Authorize(Roles = "Administrador")]
public class IndexModel : PageModel
{
    private readonly IZonasCoberturaApi _zonasApi;

    public IndexModel(IZonasCoberturaApi zonasApi)
    {
        _zonasApi = zonasApi;
    }

    [BindProperty(SupportsGet = true)]
    public string? Busqueda { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Estado { get; set; } = "todos";

    public IReadOnlyList<ZonaCoberturaDto> Zonas { get; set; } = Array.Empty<ZonaCoberturaDto>();

    public int TotalZonasActivas { get; set; }
    public int TotalZonasInactivas { get; set; }
    public int TotalFranjasConfiguradas { get; set; }

    public string? MensajeExito { get; set; }
    public string? MensajeError { get; set; }

    public async Task OnGetAsync()
    {
        MensajeExito = TempData["MensajeExito"]?.ToString();
        MensajeError = TempData["MensajeError"]?.ToString();

        // Obtenemos el conjunto global para el cálculo de métricas y contadores de pestañas
        var todasLasZonas = await _zonasApi.ListarZonasAsync(new FiltroZonasDto { TextoBusqueda = Busqueda });
        TotalZonasActivas = todasLasZonas.Count(z => z.Activa);
        TotalZonasInactivas = todasLasZonas.Count(z => !z.Activa);
        TotalFranjasConfiguradas = todasLasZonas.Where(z => z.Activa).Sum(z => z.TotalFranjas);

        // Determinamos el subconjunto a mostrar según el panel/pestaña activo
        Zonas = Estado switch
        {
            "inactivas" => todasLasZonas.Where(z => !z.Activa).ToList(),
            "activas" => todasLasZonas.Where(z => z.Activa).ToList(),
            _ => todasLasZonas
        };
    }

    /// <summary>
    /// Acción directa para inactivar o reactivar una zona de cobertura con reflejo inmediato.
    /// Al inactivar, se redirige al panel de zonas inactivas para mostrar el estado y permitir su gestión.
    /// </summary>
    public async Task<IActionResult> OnPostCambiarEstadoAsync(Guid id, bool nuevoEstado)
    {
        try
        {
            await _zonasApi.CambiarEstadoZonaAsync(id, nuevoEstado);
            TempData["MensajeExito"] = nuevoEstado
                ? "La zona de cobertura y sus franjas asociadas fueron reactivadas exitosamente."
                : "La zona de cobertura fue dada de baja (inactivada) correctamente.";
        }
        catch (Exception ex)
        {
            TempData["MensajeError"] = $"Error al modificar el estado de la zona: {ex.Message}";
        }

        // Si se inactiva, abrir el panel activo de zonas inactivas para que el usuario verifique la baja
        var estadoDestino = nuevoEstado ? "activas" : "inactivas";
        return RedirectToPage(new { Busqueda, Estado = estadoDestino });
    }
}
