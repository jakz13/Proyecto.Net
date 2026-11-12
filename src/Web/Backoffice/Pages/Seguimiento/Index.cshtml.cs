using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Seguimiento;

namespace UltimaMilla.Backoffice.Pages.Seguimiento;

/// <summary>
/// Vista pública y sin autenticación de seguimiento para el destinatario final (UC-33 y UC-34).
/// Permite abrir directamente los enlaces enviados por correo electrónico (/seguimiento/{codigo})
/// mostrando el estado actual, historial de eventos, identidad visual del operador y reprogramación.
/// </summary>
[AllowAnonymous]
public class IndexModel : PageModel
{
    private readonly ISeguimientoApi _seguimientoApi;
    private readonly IIdentidadVisualApi _identidadApi;

    public IndexModel(ISeguimientoApi seguimientoApi, IIdentidadVisualApi identidadApi)
    {
        _seguimientoApi = seguimientoApi;
        _identidadApi = identidadApi;
    }

    [BindProperty(SupportsGet = true)]
    public string? Codigo { get; set; }

    public IdentidadVisualDto Identidad { get; set; } = new();
    public SeguimientoPublicoDto? Seguimiento { get; set; }
    public IReadOnlyList<FranjaDisponibleDto> FranjasDisponibles { get; set; } = Array.Empty<FranjaDisponibleDto>();

    public string? MensajeError { get; set; }
    public string? MensajeExito { get; set; }

    [BindProperty]
    public Guid FranjaSeleccionadaId { get; set; }

    public async Task OnGetAsync(string? codigo, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(codigo))
        {
            Codigo = codigo.Trim();
        }

        await CargarDatosAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostReprogramarAsync(string codigo, CancellationToken cancellationToken)
    {
        Codigo = codigo?.Trim();
        if (string.IsNullOrWhiteSpace(Codigo) || FranjaSeleccionadaId == Guid.Empty)
        {
            MensajeError = "Debe seleccionar una franja horaria válida para solicitar la reprogramación.";
            await CargarDatosAsync(cancellationToken);
            return Page();
        }

        try
        {
            var resultado = await _seguimientoApi.SolicitarReprogramacionAsync(Codigo, FranjaSeleccionadaId, cancellationToken);
            if (resultado.Aceptada)
            {
                MensajeExito = $"Solicitud aceptada ({resultado.Estado}). Nueva franja asignada: {resultado.FranjaAsignada ?? "Confirmada"}.";
            }
            else
            {
                MensajeError = resultado.Motivo ?? "No fue posible aprobar la reprogramación según las reglas operativas.";
            }
        }
        catch (Exception ex)
        {
            MensajeError = ex.Message;
        }

        await CargarDatosAsync(cancellationToken);
        return Page();
    }

    private async Task CargarDatosAsync(CancellationToken cancellationToken)
    {
        try
        {
            Identidad = await _identidadApi.ObtenerIdentidadActualAsync(cancellationToken);
        }
        catch
        {
            Identidad = new IdentidadVisualDto();
        }

        if (string.IsNullOrWhiteSpace(Codigo))
        {
            return;
        }

        try
        {
            Seguimiento = await _seguimientoApi.ConsultarSeguimientoAsync(Codigo, cancellationToken);
            if (Seguimiento is null)
            {
                MensajeError ??= $"No se encontró un envío o retiro registrado con el código '{Codigo}'.";
                return;
            }

            Identidad = Identidad with
            {
                NombreComercial = !string.IsNullOrWhiteSpace(Seguimiento.OperadorNombre)
                    ? Seguimiento.OperadorNombre
                    : Identidad.NombreComercial,
                ColorPrimario = !string.IsNullOrWhiteSpace(Seguimiento.OperadorColorPrimario)
                    ? Seguimiento.OperadorColorPrimario
                    : Identidad.ColorPrimario,
                ColorSecundario = !string.IsNullOrWhiteSpace(Seguimiento.OperadorColorSecundario)
                    ? Seguimiento.OperadorColorSecundario
                    : Identidad.ColorSecundario,
                LogoUrl = !string.IsNullOrWhiteSpace(Seguimiento.OperadorLogoUrl)
                    ? Seguimiento.OperadorLogoUrl
                    : Identidad.LogoUrl
            };

            if (Seguimiento.PermiteReprogramacion)
            {
                try
                {
                    var respuestaFranjas = await _seguimientoApi.ObtenerFranjasDisponiblesAsync(Codigo, cancellationToken);
                    FranjasDisponibles = respuestaFranjas?.Franjas ?? Array.Empty<FranjaDisponibleDto>();
                }
                catch
                {
                    FranjasDisponibles = Array.Empty<FranjaDisponibleDto>();
                }
            }
        }
        catch (Exception ex)
        {
            MensajeError ??= ex.Message;
        }
    }
}
