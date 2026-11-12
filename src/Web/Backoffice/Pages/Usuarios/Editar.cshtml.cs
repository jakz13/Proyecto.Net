using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Contracts.Modelos.Usuarios;

namespace UltimaMilla.Backoffice.Pages.Usuarios;

[Authorize(Roles = "Administrador")]
public class EditarModel : PageModel
{
    private readonly IUsuariosApi _usuariosApi;
    private readonly IFlotaApi _flotaApi;

    public EditarModel(IUsuariosApi usuariosApi, IFlotaApi flotaApi)
    {
        _usuariosApi = usuariosApi;
        _flotaApi = flotaApi;
    }

    [BindProperty]
    public Guid Id { get; set; }

    [BindProperty]
    public ActualizarUsuarioDto Usuario { get; set; } = new();

    public string? NombreComercio { get; set; }
    public IReadOnlyList<PerfilDto> Perfiles { get; set; } = Array.Empty<PerfilDto>();
    public IReadOnlyList<VehiculoDto> Vehiculos { get; set; } = Array.Empty<VehiculoDto>();

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Id = id;
        var entidad = await _usuariosApi.ObtenerPorIdAsync(id);
        if (entidad == null)
        {
            return NotFound();
        }

        var partesNombre = (entidad.Nombre ?? string.Empty).Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var primerNombre = partesNombre.Length > 0 ? partesNombre[0] : string.Empty;
        var apellido = partesNombre.Length > 1 ? partesNombre[1] : string.Empty;

        await CargarCatalogosAsync();
        RepartidorDto? repartidorAsociado = null;
        try
        {
            var repartidores = await _flotaApi.ListarRepartidoresAsync();
            repartidorAsociado = repartidores.FirstOrDefault(r => r.UsuarioId == id);
        }
        catch
        {
            // Ignorar si falla la consulta auxiliar de flota
        }

        Usuario = new ActualizarUsuarioDto
        {
            Nombre = primerNombre,
            Apellido = apellido,
            Email = entidad.Email,
            PerfilId = entidad.PerfilId,
            Activo = entidad.Activo,
            Documento = repartidorAsociado?.Documento,
            Telefono = repartidorAsociado?.Telefono,
            VehiculoAsignadoId = repartidorAsociado?.VehiculoAsignadoId
        };

        NombreComercio = entidad.NombreComercio;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await CargarCatalogosAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _usuariosApi.ActualizarUsuarioAsync(Id, Usuario);
            TempData["MensajeExito"] = $"Los datos del usuario '{Usuario.NombreCompleto}' y su perfil fueron actualizados exitosamente.";
            return RedirectToPage("/Comercios/Index", new { tab = "usuarios" });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al actualizar el usuario: {ex.Message}";
            return Page();
        }
    }

    private async Task CargarCatalogosAsync()
    {
        Perfiles = await _usuariosApi.ListarPerfilesAsync();
        try
        {
            Vehiculos = await _flotaApi.ListarVehiculosAsync(soloActivos: true);
        }
        catch
        {
            Vehiculos = Array.Empty<VehiculoDto>();
        }
    }
}
