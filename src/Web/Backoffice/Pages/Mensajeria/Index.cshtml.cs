using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Mensajeria;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.Mensajeria;

/// <summary>
/// Modelo de página para el caso de uso UC-42: Reintentar mensajes fallidos de la cola.
/// Permite al Administrador consultar la cola interna de mensajes de la plataforma retenidos tras agotar
/// sus reintentos automáticos, inspeccionar su causa y solicitar el reintento manual individual o por lotes.
/// </summary>
[Authorize(Roles = "Administrador")]
public class IndexModel : PageModel
{
    private readonly IMensajeriaApi _mensajeriaApi;

    public IndexModel(IMensajeriaApi mensajeriaApi)
    {
        _mensajeriaApi = mensajeriaApi;
    }

    public IReadOnlyList<MensajeFallidoDto> Mensajes { get; set; } = Array.Empty<MensajeFallidoDto>();

    [BindProperty(SupportsGet = true)]
    public string? Busqueda { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TipoFiltro { get; set; } = "todos";

    [BindProperty(SupportsGet = true)]
    public string? EstadoFiltro { get; set; } = "todos";

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    // Resumen de Métricas de la Cola
    public int TotalRetenidos => Mensajes.Count(m => m.Estado == "Fallido");
    public int TotalResueltos => Mensajes.Count(m => m.Estado == "Resuelto");
    public int TiposEventosAfectados => Mensajes.Where(m => m.Estado == "Fallido").Select(m => m.TipoMensaje).Distinct().Count();
    public int TotalIntentosAcumulados => Mensajes.Sum(m => m.Intentos);

    public async Task OnGetAsync()
    {
        await CargarMensajesAsync();
    }

    /// <summary>
    /// Reintenta en lote los mensajes seleccionados mediante casillas de verificación.
    /// Soporta tanto el flujo principal (éxito) como el alternativo (reintento fallido).
    /// </summary>
    public async Task<IActionResult> OnPostReintentarLoteAsync([FromForm] List<Guid> idsSeleccionados, [FromForm] bool simularFallo = false)
    {
        if (idsSeleccionados == null || idsSeleccionados.Count == 0)
        {
            MensajeError = "Debes seleccionar al menos un mensaje de la cola para solicitar su reintento.";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }

        try
        {
            var resultado = await _mensajeriaApi.ReintentarMensajesAsync(idsSeleccionados, simularFallo);

            if (simularFallo)
            {
                MensajeError = $"[Flujo Alternativo] {resultado.MensajeResumen}";
            }
            else
            {
                MensajeExito = resultado.MensajeResumen;
            }

            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
        catch (ApiExcepcion ex)
        {
            MensajeError = ex.Message;
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al reintentar mensajes: {ex.Message}";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
    }

    /// <summary>
    /// Reintenta un único mensaje específico directamente desde su fila.
    /// </summary>
    public async Task<IActionResult> OnPostReintentarIndividualAsync(Guid id, bool simularFallo = false)
    {
        try
        {
            var resultado = await _mensajeriaApi.ReintentarMensajesAsync(new[] { id }, simularFallo);

            if (simularFallo)
            {
                MensajeError = $"[Flujo Alternativo] El mensaje volvió a fallar y permanece en la cola con intentos incrementados.";
            }
            else
            {
                MensajeExito = $"Mensaje reencolado exitosamente para su procesamiento inmediato.";
            }

            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al reintentar mensaje: {ex.Message}";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
    }

    /// <summary>
    /// Elimina definitivamente un mensaje de la cola de fallidos.
    /// </summary>
    public async Task<IActionResult> OnPostEliminarAsync(Guid id)
    {
        try
        {
            var eliminado = await _mensajeriaApi.DescartarMensajeAsync(id);
            if (eliminado)
            {
                MensajeExito = "El mensaje fue eliminado de la lista.";
            }
            else
            {
                MensajeError = "No se pudo eliminar el mensaje indicado.";
            }

            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al eliminar mensaje: {ex.Message}";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
    }

    /// <summary>
    /// Elimina en lote todos los mensajes que ya fueron resueltos con éxito.
    /// </summary>
    public async Task<IActionResult> OnPostLimpiarResueltosAsync()
    {
        try
        {
            var eliminados = await _mensajeriaApi.LimpiarMensajesResueltosAsync();
            MensajeExito = eliminados > 0
                ? $"Se eliminaron {eliminados} mensaje(s) resueltos de la cola."
                : "No había mensajes resueltos pendientes de eliminar.";

            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al limpiar mensajes resueltos: {ex.Message}";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
    }

    private async Task CargarMensajesAsync()
    {
        Mensajes = await _mensajeriaApi.ListarMensajesFallidosAsync(
            tipo: TipoFiltro,
            estado: EstadoFiltro,
            busqueda: Busqueda);
    }
}
