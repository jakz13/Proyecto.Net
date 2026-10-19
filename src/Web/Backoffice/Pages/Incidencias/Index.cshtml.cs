using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Incidencias;

namespace UltimaMilla.Backoffice.Pages.Incidencias;

/// <summary>
/// Modelo de página para la consulta, registro, seguimiento y cierre de incidencias operativas (UC-11).
/// </summary>
[Authorize(Roles = "Despachador,Administrador")]
public class IndexModel : PageModel
{
    private readonly IIncidenciasApi _incidenciasApi;

    public IndexModel(IIncidenciasApi incidenciasApi)
    {
        _incidenciasApi = incidenciasApi;
    }

    public IReadOnlyList<IncidenciaDto> Incidencias { get; private set; } = new List<IncidenciaDto>();
    public IReadOnlyList<EnvioDisponibleIncidenciaDto> EnviosDisponibles { get; private set; } = new List<EnvioDisponibleIncidenciaDto>();
    public IReadOnlyList<RutaDisponibleIncidenciaDto> RutasDisponibles { get; private set; } = new List<RutaDisponibleIncidenciaDto>();

    // Contadores para métricas
    public int TotalIncidencias { get; private set; }
    public int CantidadAbiertas { get; private set; }
    public int CantidadEnInvestigacion { get; private set; }
    public int CantidadCerradas { get; private set; }

    // Filtros activos
    [BindProperty(SupportsGet = true)]
    public string EstadoFiltro { get; set; } = "Todos";

    [BindProperty(SupportsGet = true)]
    public string? TipoFiltro { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SeveridadFiltro { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Busqueda { get; set; }

    // Formularios de acción
    [BindProperty]
    public RegistrarIncidenciaDto NuevaIncidencia { get; set; } = new();

    [BindProperty]
    public AgregarSeguimientoIncidenciaDto SeguimientoInput { get; set; } = new();

    [BindProperty]
    public CerrarIncidenciaDto CierreInput { get; set; } = new();

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancelacion)
    {
        var filtro = new FiltroIncidenciasDto
        {
            Estado = EstadoFiltro,
            Tipo = TipoFiltro,
            Severidad = SeveridadFiltro,
            Busqueda = Busqueda
        };

        var todas = await _incidenciasApi.ListarIncidenciasAsync(null, cancelacion);
        TotalIncidencias = todas.Count;
        CantidadAbiertas = todas.Count(i => i.Estado == "Abierta");
        CantidadEnInvestigacion = todas.Count(i => i.Estado == "EnInvestigacion");
        CantidadCerradas = todas.Count(i => i.Estado == "Cerrada" || i.Estado == "Resuelta");

        Incidencias = await _incidenciasApi.ListarIncidenciasAsync(filtro, cancelacion);
        EnviosDisponibles = await _incidenciasApi.ListarEnviosDisponiblesAsync(cancelacion);
        RutasDisponibles = await _incidenciasApi.ListarRutasDisponiblesAsync(cancelacion);

        return Page();
    }

    public async Task<IActionResult> OnPostRegistrarAsync(CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(NuevaIncidencia.Descripcion))
        {
            MensajeError = "Debe proporcionar una descripción detallada de la incidencia.";
            return RedirectToPage();
        }

        var usuario = User.Identity?.Name ?? "Despachador";
        var creada = await _incidenciasApi.RegistrarIncidenciaAsync(NuevaIncidencia, usuario, cancelacion);

        MensajeExito = $"Incidencia {creada.NumeroIncidencia} registrada exitosamente.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSeguimientoAsync(CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(SeguimientoInput.Comentario))
        {
            MensajeError = "Debe ingresar una nota de seguimiento o actualización.";
            return RedirectToPage();
        }

        var usuario = User.Identity?.Name ?? "Despachador";
        var resultado = await _incidenciasApi.AgregarSeguimientoAsync(SeguimientoInput, usuario, cancelacion);

        if (resultado)
        {
            MensajeExito = "Seguimiento incorporado correctamente a la bitácora de la incidencia.";
        }
        else
        {
            MensajeError = "No se encontró la incidencia indicada para agregar el seguimiento.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCerrarAsync(CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(CierreInput.ResolucionDetalle))
        {
            MensajeError = "Debe especificar la justificación o detalle de la resolución antes de cerrar la incidencia.";
            return RedirectToPage();
        }

        var usuario = User.Identity?.Name ?? "Despachador";
        var resultado = await _incidenciasApi.CerrarIncidenciaAsync(CierreInput, usuario, cancelacion);

        if (resultado)
        {
            MensajeExito = "La incidencia fue cerrada y marcada como resuelta.";
        }
        else
        {
            MensajeError = "No se pudo cerrar la incidencia especificada.";
        }

        return RedirectToPage();
    }
}
