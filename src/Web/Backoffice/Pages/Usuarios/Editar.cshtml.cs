using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Usuarios;

namespace UltimaMilla.Backoffice.Pages.Usuarios;

[Authorize(Roles = "Administrador")]
public class EditarModel : PageModel
{
    private readonly IUsuariosApi _usuariosApi;

    public EditarModel(IUsuariosApi usuariosApi)
    {
        _usuariosApi = usuariosApi;
    }

    [BindProperty]
    public Guid Id { get; set; }

    [BindProperty]
    public ActualizarUsuarioDto Usuario { get; set; } = new();

    public string? NombreComercio { get; set; }
    public IReadOnlyList<PerfilDto> Perfiles { get; set; } = Array.Empty<PerfilDto>();

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Id = id;
        var entidad = await _usuariosApi.ObtenerPorIdAsync(id);
        if (entidad == null)
        {
            return NotFound();
        }

        Usuario = new ActualizarUsuarioDto
        {
            Nombre = entidad.Nombre,
            Email = entidad.Email,
            PerfilId = entidad.PerfilId,
            Activo = entidad.Activo
        };

        NombreComercio = entidad.NombreComercio;
        Perfiles = await _usuariosApi.ListarPerfilesAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Perfiles = await _usuariosApi.ListarPerfilesAsync();
            return Page();
        }

        try
        {
            await _usuariosApi.ActualizarUsuarioAsync(Id, Usuario);
            TempData["MensajeExito"] = $"Los datos del usuario '{Usuario.Nombre}' y su perfil fueron actualizados exitosamente.";
            return RedirectToPage("/Comercios/Index", new { tab = "usuarios" });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al actualizar el usuario: {ex.Message}";
            Perfiles = await _usuariosApi.ListarPerfilesAsync();
            return Page();
        }
    }
}
