using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Comercios;
using UltimaMilla.Frontend.Contracts.Modelos.Usuarios;

namespace UltimaMilla.Backoffice.Pages.Usuarios;

[Authorize(Roles = "Administrador")]
public class NuevoModel : PageModel
{
    private readonly IUsuariosApi _usuariosApi;
    private readonly IComerciosApi _comerciosApi;

    public NuevoModel(IUsuariosApi usuariosApi, IComerciosApi comerciosApi)
    {
        _usuariosApi = usuariosApi;
        _comerciosApi = comerciosApi;
    }

    [BindProperty]
    public CrearUsuarioDto NuevoUsuario { get; set; } = new();

    public IReadOnlyList<PerfilDto> Perfiles { get; set; } = Array.Empty<PerfilDto>();
    public IReadOnlyList<ComercioDto> Comercios { get; set; } = Array.Empty<ComercioDto>();

    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await CargarCatalogosAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await CargarCatalogosAsync();
            return Page();
        }

        try
        {
            await _usuariosApi.CrearUsuarioAsync(NuevoUsuario);
            TempData["MensajeExito"] = $"El usuario '{NuevoUsuario.Nombre}' fue dado de alta exitosamente con perfil asignado.";
            return RedirectToPage("/Comercios/Index", new { tab = "usuarios" });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al registrar usuario: {ex.Message}";
            await CargarCatalogosAsync();
            return Page();
        }
    }

    private async Task CargarCatalogosAsync()
    {
        Perfiles = await _usuariosApi.ListarPerfilesAsync();
        var resultadoComercios = await _comerciosApi.ListarComerciosAsync(new FiltroComerciosDto
        {
            TamanoPagina = 100,
            SoloActivos = true
        });
        Comercios = resultadoComercios.Elementos;
    }
}
