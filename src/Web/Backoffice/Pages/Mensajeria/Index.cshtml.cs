using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Mensajeria;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.Mensajeria;

/// <summary>
/// Modelo de página para el caso de uso Reintentar mensajes fallidos de la cola.
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

    // Resumen de Métricas de la Cola según Vocabulario Canónico
    public int TotalPendientes => Mensajes.Count(m => m.EstadoCanonico == "Pendiente");
    public int TotalReintentados => Mensajes.Count(m => m.EstadoCanonico == "Reintentado");
    public int TotalDescartados => Mensajes.Count(m => m.EstadoCanonico == "Descartado");
    public int TotalIntentosAcumulados => Mensajes.Sum(m => m.Intentos);

    // Compatibilidad para vistas previas
    public int TotalRetenidos => TotalPendientes;
    public int TotalResueltos => TotalReintentados;
    public int TiposEventosAfectados => Mensajes.Where(m => m.EstadoCanonico == "Pendiente").Select(m => m.TipoMensaje).Distinct().Count();

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
                MensajeError = $"[Simulación]: {resultado.MensajeResumen}";
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
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            MensajeError = "Conflicto: uno o varios mensajes ya estaban siendo procesados por otra instancia.";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            MensajeError = "Acceso denegado: uno o más mensajes pertenecen a otro operador logístico.";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al reintentar mensajes: {ex.Message}";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
    }

    /// <summary>
    /// Reintenta un único mensaje específico directamente desde su fila o panel lateral.
    /// </summary>
    public async Task<IActionResult> OnPostReintentarIndividualAsync(Guid id, bool simularFallo = false)
    {
        try
        {
            var resultado = await _mensajeriaApi.ReintentarMensajesAsync(new[] { id }, simularFallo);

            if (simularFallo)
            {
                MensajeError = "[Simulación]: El reintento falló nuevamente y el mensaje permanece en la cola con intentos actualizados.";
            }
            else
            {
                MensajeExito = "Mensaje reencolado exitosamente para su procesamiento inmediato.";
            }

            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            MensajeError = "El mensaje no se encontró en la cola (puede haber sido procesado recientemente).";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            MensajeError = "Acceso denegado: el mensaje solicitado no corresponde al operador actual.";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al reintentar mensaje: {ex.Message}";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
    }

    /// <summary>
    /// Descarta lógicamente un mensaje de la cola de fallidos registrando el motivo obligatorio.
    /// </summary>
    public async Task<IActionResult> OnPostDescartarAsync(Guid id, string motivo)
    {
        if (id == Guid.Empty)
        {
            MensajeError = "El identificador del mensaje a descartar es inválido.";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            MensajeError = "Debes ingresar un motivo obligatorio para descartar el mensaje de la cola.";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }

        try
        {
            var descartado = await _mensajeriaApi.DescartarMensajeAsync(id, motivo);
            if (descartado)
            {
                MensajeExito = "El mensaje fue descartado lógicamente y archivado con su motivo en el historial.";
            }
            else
            {
                MensajeError = "No se encontró el mensaje o no se pudo descartar.";
            }

            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            MensajeError = "El mensaje no existe en la cola de fallidos.";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            MensajeError = "Acceso denegado: no puedes descartar mensajes de otro operador.";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al descartar mensaje: {ex.Message}";
            return RedirectToPage(new { Busqueda, TipoFiltro, EstadoFiltro });
        }
    }

    /// <summary>
    /// Manejador de descarte por compatibilidad con nombres antiguos.
    /// </summary>
    public Task<IActionResult> OnPostEliminarAsync(Guid id, string? motivo = null)
    {
        return OnPostDescartarAsync(id, string.IsNullOrWhiteSpace(motivo) ? "Descartado manualmente por el administrador" : motivo);
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
