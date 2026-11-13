using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Deposito;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.Deposito;

/// <summary>
/// Modelo de página para la estación de escaneo, contraste y recepción física de bultos en depósito.
/// </summary>
[Authorize(Roles = "Operario")]
public class RecepcionModel : PageModel
{
    private readonly IDepositoApi _depositoApi;

    public RecepcionModel(IDepositoApi depositoApi)
    {
        _depositoApi = depositoApi ?? throw new ArgumentNullException(nameof(depositoApi));
    }

    public EnvioAdmitidoDto? Envio { get; private set; }

    [BindProperty]
    public FormularioRecepcionDto Formulario { get; set; } = new();

    public ResultadoRecepcionDto? Resultado { get; private set; }

    public List<string> ErroresValidacion { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? CodigoEscaneado { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid? id, string? codigo, string? codigoEscaneado, CancellationToken cancelacion)
    {
        CodigoEscaneado = codigoEscaneado ?? codigo;

        if (id.HasValue && id.Value != Guid.Empty)
        {
            Envio = await _depositoApi.ObtenerEnvioParaRecepcionAsync(id.Value, cancelacion);
        }
        else if (!string.IsNullOrWhiteSpace(CodigoEscaneado))
        {
            var todos = await _depositoApi.ListarEnviosAdmitidosAsync(cancelacion);
            var termino = CodigoEscaneado.Trim();
            Envio = todos.FirstOrDefault(e =>
                e.CodigoSeguimiento.Equals(termino, StringComparison.OrdinalIgnoreCase) ||
                e.Bultos.Any(b => b.Identificacion.Equals(termino, StringComparison.OrdinalIgnoreCase) ||
                                  $"{e.CodigoSeguimiento}-{b.Identificacion}".Equals(termino, StringComparison.OrdinalIgnoreCase)));
        }

        if (Envio == null)
        {
            TempData["MensajeError"] = "No se encontró un envío en estado 'Admitido' con los datos ingresados.";
            return RedirectToPage("/Deposito/Index");
        }

        Formulario.EnvioId = Envio.EnvioId;
        Formulario.CodigosEscaneadosCsv = string.Join(",", Envio.Bultos.Select(b => b.Identificacion));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancelacion)
    {
        Envio = await _depositoApi.ObtenerEnvioParaRecepcionAsync(Formulario.EnvioId, cancelacion);
        if (Envio == null)
        {
            TempData["MensajeError"] = "El envío no existe o ya no se encuentra en estado 'Admitido'.";
            return RedirectToPage("/Deposito/Index");
        }

        var escaneados = (Formulario.CodigosEscaneadosCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var danados = (Formulario.CodigosDanadosCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        // El formulario siempre parte con todos los bultos marcados: una lista vacía significa que el operario los desmarcó todos
        // (no llegó ninguno). No se vuelve a "dar por recibidos" en silencio: eso registraba como recibido un envío que no estaba.
        if (escaneados.Count == 0)
        {
            ErroresValidacion.Add("No hay ningún bulto marcado como recibido: marque o escanee los bultos arribados. Si el envío no llegó, no se puede recepcionar; declare el extravío desde Envíos.");
            return Page();
        }

        var solicitud = new RecepcionarBultosRequestDto(
            escaneados,
            danados.Count > 0 ? danados : null,
            string.IsNullOrWhiteSpace(Formulario.Observaciones) ? null : Formulario.Observaciones.Trim());

        try
        {
            Resultado = await _depositoApi.RecepcionarBultosAsync(Formulario.EnvioId, solicitud, cancelacion);

            if (!Resultado.Recepcionado)
            {
                ErroresValidacion.Add(Resultado.Mensaje);
                return Page();
            }

            TempData["MensajeExito"] = Resultado.Mensaje;
            return Page();
        }
        catch (ApiExcepcion ex)
        {
            ErroresValidacion.Add(ex.Message);
            return Page();
        }
    }
}

public class FormularioRecepcionDto
{
    public Guid EnvioId { get; set; }
    public string? CodigosEscaneadosCsv { get; set; }
    public string? CodigosDanadosCsv { get; set; }
    public string? Observaciones { get; set; }
}
