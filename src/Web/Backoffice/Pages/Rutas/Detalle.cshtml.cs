using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.MotivosNoEntrega;
using UltimaMilla.Frontend.Contracts.Modelos.Rutas;

namespace UltimaMilla.Backoffice.Pages.Rutas;

/// <summary>
/// Controlador de la vista de detalle de hoja de ruta y reasignación / reprogramación de envíos (UC-10, RF-12, RF-13, RF-15).
/// </summary>
[Authorize(Roles = "Despachador,Administrador")]
public class DetalleModel : PageModel
{
    private readonly IRutasApi _rutasApi;
    private readonly IMotivosNoEntregaApi _motivosNoEntregaApi;

    public DetalleModel(IRutasApi rutasApi, IMotivosNoEntregaApi motivosNoEntregaApi)
    {
        _rutasApi = rutasApi;
        _motivosNoEntregaApi = motivosNoEntregaApi;
    }

    public HojaDeRutaDto? HojaDeRuta { get; private set; }

    /// <summary>
    /// Otras hojas de ruta activas (liberadas o borrador) candidatas a recibir un envío reasignado.
    /// </summary>
    public IReadOnlyList<HojaDeRutaDto> OtrasRutasActivas { get; private set; } = new List<HojaDeRutaDto>();

    /// <summary>
    /// Catálogo interno de motivos de no entrega para incidencias en ruta despachada.
    /// </summary>
    public IReadOnlyList<MotivoNoEntregaDto> MotivosCatalogo { get; private set; } = new List<MotivoNoEntregaDto>();

    /// <summary>
    /// Historial de auditoría de reasignaciones e incidencias de esta hoja de ruta (RF-12, RF-15).
    /// </summary>
    public IReadOnlyList<EventoReasignacionDto> HistorialEventos { get; private set; } = new List<EventoReasignacionDto>();

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    [TempData]
    public string? NotificacionRepartidor { get; set; }

    [BindProperty]
    public ReasignarEnvioDto ReasignacionInput { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            return RedirectToPage("/Rutas/Index");
        }

        HojaDeRuta = await _rutasApi.ObtenerHojaDeRutaPorIdAsync(id);
        if (HojaDeRuta == null)
        {
            return RedirectToPage("/Rutas/Index");
        }

        await CargarDatosComplementariosAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostReasignarAsync(Guid id)
    {
        return await ProcesarReasignacionAsync(id);
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        return await ProcesarReasignacionAsync(id);
    }

    private async Task<IActionResult> ProcesarReasignacionAsync(Guid id)
    {
        if (id != Guid.Empty && ReasignacionInput.HojaDeRutaActualId == Guid.Empty)
        {
            ReasignacionInput = ReasignacionInput with { HojaDeRutaActualId = id };
        }

        if (ReasignacionInput.HojaDeRutaActualId == Guid.Empty || ReasignacionInput.EnvioId == Guid.Empty)
        {
            MensajeError = "Datos de envío u hoja de ruta incompletos para procesar la acción.";
            return RedirectToPage("/Rutas/Detalle", new { id });
        }

        var despachadorUsuario = User.Identity?.Name ?? "despachador.central";

        var resultado = await _rutasApi.ReasignarOReprogramarEnvioAsync(
            ReasignacionInput,
            despachadorUsuario,
            HttpContext.RequestAborted);

        if (resultado.Exitoso)
        {
            MensajeExito = resultado.Mensaje;
            NotificacionRepartidor = resultado.NotificacionRepartidor;
        }
        else
        {
            MensajeError = resultado.Mensaje;
        }

        return RedirectToPage("/Rutas/Detalle", new { id = ReasignacionInput.HojaDeRutaActualId });
    }

    private async Task CargarDatosComplementariosAsync(Guid id)
    {
        // 1. Obtener otras rutas activas donde reasignar envíos
        var todasLasRutas = await _rutasApi.ListarHojasDeRutaAsync();
        OtrasRutasActivas = todasLasRutas
            .Where(r => r.Id != id && (r.Estado == "Liberada" || r.Estado == "Borrador"))
            .ToList();

        // 2. Obtener catálogo oficial de motivos de no entrega activos
        var motivos = await _motivosNoEntregaApi.ListarMotivosAsync();
        MotivosCatalogo = motivos.Where(m => m.Activo).ToList();

        // 3. Obtener log histórico de eventos de reasignación
        HistorialEventos = await _rutasApi.ListarEventosReasignacionAsync(id);
    }
}
