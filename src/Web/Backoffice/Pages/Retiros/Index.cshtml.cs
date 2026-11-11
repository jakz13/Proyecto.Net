using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Backoffice.Seguridad;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Retiros;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.Retiros;

/// <summary>
/// Retiros en domicilio del operador (cambio de requerimientos 2.2, RF 38 y RF 41): seguimiento por estado y acciones del operador
/// (recibir en depósito, entregar al comercio, reprogramar, sacar de la ruta y cancelar).
/// </summary>
[Authorize(Roles = "Despachador,Operario")]
public class IndexModel : PageModel
{
    private readonly IRetirosApi _retirosApi;

    public IndexModel(IRetirosApi retirosApi)
    {
        _retirosApi = retirosApi;
    }

    public ResumenRetirosOperadorDto Resumen { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Estado { get; set; }

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    public async Task OnGetAsync() => Resumen = await _retirosApi.ListarDelOperadorAsync(Estado);

    public Task<IActionResult> OnPostRecibirAsync(Guid id) =>
        !PermisosBackoffice.PuedeTrabajarEnDeposito(User) ? Task.FromResult<IActionResult>(Forbid()) : EjecutarAsync(() => _retirosApi.RecibirEnDepositoAsync(id), "Los bultos se recibieron en depósito.");

    public Task<IActionResult> OnPostEntregarAsync(Guid id, string? receptor) =>
        !PermisosBackoffice.PuedeTrabajarEnDeposito(User) ? Task.FromResult<IActionResult>(Forbid()) : EjecutarAsync(() => _retirosApi.EntregarAlComercioAsync(id, receptor), "Los bultos se entregaron al comercio y el retiro quedó cerrado.");

    // Reprogramar, sacar de la ruta y cancelar son decisiones de planificación: el operario de depósito no las toma
    public Task<IActionResult> OnPostReprogramarAsync(Guid id) =>
        !PermisosBackoffice.PuedeOperar(User) ? Task.FromResult<IActionResult>(Forbid()) : EjecutarAsync(() => _retirosApi.ReprogramarAsync(id), "El retiro volvió a quedar solicitado para una nueva visita.");

    public Task<IActionResult> OnPostSacarDeRutaAsync(Guid id) =>
        !PermisosBackoffice.PuedeOperar(User) ? Task.FromResult<IActionResult>(Forbid()) : EjecutarAsync(() => _retirosApi.SacarDeRutaAsync(id), "El retiro se sacó de la hoja de ruta.");

    public Task<IActionResult> OnPostCancelarAsync(Guid id, string? motivo) =>
        !PermisosBackoffice.PuedeOperar(User) ? Task.FromResult<IActionResult>(Forbid()) : EjecutarAsync(() => _retirosApi.CancelarDelOperadorAsync(id, motivo ?? string.Empty), "El retiro se canceló.");

    private async Task<IActionResult> EjecutarAsync(Func<Task<RetiroDetalleDto>> accion, string exito)
    {
        try
        {
            await accion();
            MensajeExito = exito;
        }
        catch (ApiExcepcion ex)
        {
            MensajeError = ex.Message;
        }

        return RedirectToPage(new { Estado });
    }
}
