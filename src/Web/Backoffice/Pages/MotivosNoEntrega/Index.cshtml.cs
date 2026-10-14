using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.MotivosNoEntrega;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.MotivosNoEntrega;

/// <summary>
/// Modelo de página para el caso de uso UC-05: Gestionar catálogo de motivos de no entrega.
/// Permite al Administrador dar de alta, editar y activar/desactivar motivos,
/// reflejando de inmediato el catálogo que sincronizará la aplicación móvil del repartidor (RF-04, RF-21).
/// </summary>
[Authorize(Roles = "Administrador")]
public class IndexModel : PageModel
{
    private readonly IMotivosNoEntregaApi _motivosApi;

    public IndexModel(IMotivosNoEntregaApi motivosApi)
    {
        _motivosApi = motivosApi;
    }

    public IReadOnlyList<MotivoNoEntregaDto> Motivos { get; set; } = Array.Empty<MotivoNoEntregaDto>();
    public IReadOnlyList<MotivoNoEntregaDto> MotivosMovil { get; set; } = Array.Empty<MotivoNoEntregaDto>();
    public ResumenMotivosNoEntregaDto Resumen { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Busqueda { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? EstadoFiltro { get; set; } = "todos";

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    [BindProperty]
    public CrearMotivoNoEntregaDto NuevoMotivo { get; set; } = new();

    [BindProperty]
    public ActualizarMotivoNoEntregaDto MotivoEditado { get; set; } = new();

    public async Task OnGetAsync()
    {
        await CargarDatosAsync();
    }

    public async Task<IActionResult> OnPostCrearAsync()
    {
        if (!ModelState.IsValid)
        {
            MensajeError = "Por favor verifica los campos obligatorios del motivo.";
            await CargarDatosAsync();
            return Page();
        }

        try
        {
            var creado = await _motivosApi.CrearMotivoAsync(NuevoMotivo);
            MensajeExito = $"Motivo '{creado.Codigo}' dado de alta exitosamente. Disponible para sincronización con la app móvil (RF-21).";
            return RedirectToPage(new { busqueda = Busqueda, estadoFiltro = EstadoFiltro });
        }
        catch (ApiExcepcion ex)
        {
            MensajeError = ex.Message;
            await CargarDatosAsync();
            return Page();
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al dar de alta el motivo: {ex.Message}";
            await CargarDatosAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostActualizarAsync()
    {
        if (MotivoEditado.Id == Guid.Empty || string.IsNullOrWhiteSpace(MotivoEditado.Descripcion))
        {
            MensajeError = "La descripción no puede estar vacía.";
            await CargarDatosAsync();
            return Page();
        }

        try
        {
            var actualizado = await _motivosApi.ActualizarMotivoAsync(MotivoEditado);
            MensajeExito = $"Motivo '{actualizado.Codigo}' actualizado correctamente.";
            return RedirectToPage(new { busqueda = Busqueda, estadoFiltro = EstadoFiltro });
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al actualizar el motivo: {ex.Message}";
            await CargarDatosAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCambiarEstadoAsync(Guid id, bool nuevoEstado)
    {
        try
        {
            await _motivosApi.CambiarEstadoAsync(id, nuevoEstado);
            if (nuevoEstado)
            {
                MensajeExito = "Motivo activado. Se incluirá en la próxima sincronización de la app del repartidor (RF-21).";
            }
            else
            {
                MensajeExito = "Motivo desactivado. Ya no se ofrecerá al repartidor para nuevos intentos fallidos.";
            }
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al cambiar el estado del motivo: {ex.Message}";
        }

        return RedirectToPage(new { busqueda = Busqueda, estadoFiltro = EstadoFiltro });
    }

    private async Task CargarDatosAsync()
    {
        bool? soloActivos = EstadoFiltro switch
        {
            "activos" => true,
            "inactivos" => false,
            _ => null
        };

        var filtro = new FiltroMotivosNoEntregaDto
        {
            Busqueda = Busqueda,
            SoloActivos = soloActivos
        };

        Motivos = await _motivosApi.ListarMotivosAsync(filtro);
        Resumen = await _motivosApi.ObtenerResumenAsync();
        MotivosMovil = await _motivosApi.ObtenerMotivosParaSincronizacionMovilAsync();
    }
}
