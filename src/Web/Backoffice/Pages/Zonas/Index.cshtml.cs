using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;

namespace UltimaMilla.Backoffice.Pages.Zonas;

/// <summary>
/// Vista principal del caso de uso UC-02: Configurar zonas de cobertura y franjas horarias.
/// Permite visualizar polígonos, filtrar, ver horarios de entrega y alternar estado activo/inactivo (RF-02).
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
    public int TotalFranjasConfiguradas { get; set; }

    public string? MensajeExito { get; set; }
    public string? MensajeError { get; set; }

    public async Task OnGetAsync()
    {
        MensajeExito = TempData["MensajeExito"]?.ToString();
        MensajeError = TempData["MensajeError"]?.ToString();

        bool? soloActivas = Estado switch
        {
            "activas" => true,
            "inactivas" => false,
            _ => null
        };

        var filtro = new FiltroZonasDto
        {
            TextoBusqueda = Busqueda,
            SoloActivas = soloActivas
        };

        Zonas = await _zonasApi.ListarZonasAsync(filtro);

        // Métricas de cobertura
        TotalZonasActivas = Zonas.Count(z => z.Activa);
        TotalFranjasConfiguradas = Zonas.Where(z => z.Activa).Sum(z => z.TotalFranjas);
    }

    /// <summary>
    /// Acción directa para inactivar o reactivar una zona de cobertura con reflejo inmediato.
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

        return RedirectToPage(new { Busqueda, Estado });
    }
}
