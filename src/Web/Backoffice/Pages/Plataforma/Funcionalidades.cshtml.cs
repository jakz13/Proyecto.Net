using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Funcionalidades;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.Plataforma;

/// <summary>
/// Banderas de funcionalidad por operador: el administrador de la plataforma (quien ofrece el servicio) activa o desactiva
/// funcionalidades para cada operador en tiempo de ejecución, sin desplegar. Cada cambio queda registrado con quién y cuándo.
/// </summary>
[Authorize(Roles = RolPlataforma)]
public class FuncionalidadesModel : PageModel
{
    public const string RolPlataforma = "AdministradorPlataforma";

    private readonly IFuncionalidadesApi _api;

    public FuncionalidadesModel(IFuncionalidadesApi api) => _api = api;

    public IReadOnlyList<OperadorFuncionalidadesDto> Operadores { get; private set; } = Array.Empty<OperadorFuncionalidadesDto>();

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnPostCambiarAsync(Guid operadorId, string clave, bool habilitada)
    {
        try
        {
            await _api.CambiarAsync(operadorId, clave, habilitada);
            MensajeExito = habilitada ? "Funcionalidad habilitada. El cambio rige de inmediato." : "Funcionalidad deshabilitada. El cambio rige de inmediato.";
        }
        catch (ApiExcepcion ex)
        {
            MensajeError = ex.Message;
        }

        return RedirectToPage();
    }

    private async Task CargarAsync()
    {
        try
        {
            Operadores = await _api.ListarPorOperadorAsync();
        }
        catch (ApiExcepcion ex)
        {
            MensajeError = ex.Message;
        }
    }
}
