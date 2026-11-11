using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Deposito;

namespace UltimaMilla.Backoffice.Pages.Deposito;

/// <summary>
/// Modelo de página para el listado de arribos en estado Admitido pendientes de recepción física en depósito (UC-14, RF-10).
/// </summary>
[Authorize(Roles = "Operario,Despachador,Administrador")]
public class IndexModel : PageModel
{
    private readonly IDepositoApi _depositoApi;

    public IndexModel(IDepositoApi depositoApi)
    {
        _depositoApi = depositoApi ?? throw new ArgumentNullException(nameof(depositoApi));
    }

    public IReadOnlyList<EnvioAdmitidoDto> EnviosAdmitidos { get; private set; } = Array.Empty<EnvioAdmitidoDto>();

    [BindProperty(SupportsGet = true)]
    public string? TerminoBusqueda { get; set; }

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    public int TotalBultosPendientes { get; private set; }
    public decimal PesoTotalKg { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancelacion)
    {
        var todos = await _depositoApi.ListarEnviosAdmitidosAsync(cancelacion);

        if (!string.IsNullOrWhiteSpace(TerminoBusqueda))
        {
            var termino = TerminoBusqueda.Trim();

            // Si el operario escaneó una etiqueta de bulto o de envío generada por el comercio, buscar coincidencia directa
            var coincidenciaDirecta = todos.FirstOrDefault(e =>
                string.Equals(e.CodigoSeguimiento, termino, StringComparison.OrdinalIgnoreCase) ||
                e.Bultos.Any(b => string.Equals(b.Identificacion, termino, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals($"{e.CodigoSeguimiento}-{b.Identificacion}", termino, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(b.Identificacion, termino.Replace($"{e.CodigoSeguimiento}-", ""), StringComparison.OrdinalIgnoreCase)) ||
                (termino.StartsWith(e.CodigoSeguimiento, StringComparison.OrdinalIgnoreCase) && termino.Length > e.CodigoSeguimiento.Length)
            );

            if (coincidenciaDirecta != null)
            {
                // Redirección inmediata a la estación de escaneo y recepción
                return RedirectToPage("/Deposito/Recepcion", new { id = coincidenciaDirecta.EnvioId, codigoEscaneado = termino });
            }

            var terminoMin = termino.ToLowerInvariant();
            todos = todos.Where(e =>
                e.CodigoSeguimiento.ToLowerInvariant().Contains(terminoMin) ||
                e.NombreDestinatario.ToLowerInvariant().Contains(terminoMin) ||
                e.DireccionDestino.ToLowerInvariant().Contains(terminoMin) ||
                e.Bultos.Any(b => b.Identificacion.ToLowerInvariant().Contains(terminoMin))
            ).ToList();
        }

        EnviosAdmitidos = todos;
        TotalBultosPendientes = EnviosAdmitidos.Sum(e => e.CantidadBultos);
        PesoTotalKg = EnviosAdmitidos.Sum(e => e.PesoTotal);

        return Page();
    }
}
