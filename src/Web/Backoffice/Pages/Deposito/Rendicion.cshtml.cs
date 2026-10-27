using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Deposito;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.Deposito;

/// <summary>
/// Modelo de página para la rendición de repartidores al regreso a depósito (UC-16, RF-22).
/// Permite al operario contrastar bultos entregados, devueltos y sin intento, seleccionar motivos internos y cerrar la hoja de ruta.
/// </summary>
[Authorize(Roles = "Operario,Despachador,Administrador")]
public class RendicionModel : PageModel
{
    private readonly IDepositoApi _depositoApi;
    private readonly ILogger<RendicionModel> _logger;

    public RendicionModel(IDepositoApi depositoApi, ILogger<RendicionModel> logger)
    {
        _depositoApi = depositoApi ?? throw new ArgumentNullException(nameof(depositoApi));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    public IReadOnlyList<RutaPendienteRendicionDto> RutasPendientes { get; set; } = Array.Empty<RutaPendienteRendicionDto>();

    public RutaParaRendicionDetalleDto? RutaDetalle { get; set; }

    public ResultadoRendicionRutaDto? ResultadoRendicion { get; set; }

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    [BindProperty]
    public List<string> CodigosBultosDevueltos { get; set; } = new();

    [BindProperty]
    public string? MotivoInternoCodigo { get; set; }

    [BindProperty]
    public string? Observaciones { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            RutasPendientes = await _depositoApi.ListarRutasPendientesRendicionAsync();

            if (Id.HasValue && Id.Value != Guid.Empty)
            {
                RutaDetalle = await _depositoApi.ObtenerRutaParaRendicionAsync(Id.Value);
                if (RutaDetalle == null)
                {
                    MensajeError = $"La hoja de ruta '{Id.Value}' no existe o no corresponde al operador actual.";
                }
            }
        }
        catch (ApiExcepcion ex)
        {
            _logger.LogError(ex, "Error de API al consultar datos de rendición.");
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al cargar la página de rendición.");
            MensajeError = "Ocurrió un error al cargar la información de rendición de depósito.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRendirAsync()
    {
        if (!Id.HasValue || Id.Value == Guid.Empty)
        {
            MensajeError = "Identificador de hoja de ruta inválido.";
            return RedirectToPage("/Deposito/Rendicion");
        }

        try
        {
            var codigosFiltrados = (CodigosBultosDevueltos ?? new List<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();

            var solicitud = new RendirHojaDeRutaRequestDto(
                codigosFiltrados,
                string.IsNullOrWhiteSpace(MotivoInternoCodigo) ? null : MotivoInternoCodigo.Trim(),
                string.IsNullOrWhiteSpace(Observaciones) ? null : Observaciones.Trim());

            ResultadoRendicion = await _depositoApi.RendirHojaDeRutaAsync(Id.Value, solicitud);
            MensajeExito = ResultadoRendicion.Mensaje;

            // Recargar datos actualizados de la ruta (estado Rendida)
            RutaDetalle = await _depositoApi.ObtenerRutaParaRendicionAsync(Id.Value);
            RutasPendientes = await _depositoApi.ListarRutasPendientesRendicionAsync();
            return Page();
        }
        catch (ApiExcepcion ex)
        {
            _logger.LogWarning(ex, "Error al registrar la rendición de la hoja de ruta {HojaDeRutaId}.", Id.Value);
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al registrar la rendición.");
            MensajeError = "Ocurrió un error inesperado al procesar la rendición.";
        }

        // En caso de error, recargar detalle de ruta
        try
        {
            RutaDetalle = await _depositoApi.ObtenerRutaParaRendicionAsync(Id.Value);
            RutasPendientes = await _depositoApi.ListarRutasPendientesRendicionAsync();
        }
        catch
        {
            // Ignorar errores secundarios de recarga
        }

        return Page();
    }
}
