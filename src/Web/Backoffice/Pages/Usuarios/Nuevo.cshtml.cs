using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Comercios;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Contracts.Modelos.Usuarios;

namespace UltimaMilla.Backoffice.Pages.Usuarios;

[Authorize(Roles = "Administrador")]
public class NuevoModel : PageModel
{
    private readonly IUsuariosApi _usuariosApi;
    private readonly IComerciosApi _comerciosApi;
    private readonly IFlotaApi _flotaApi;

    public NuevoModel(IUsuariosApi usuariosApi, IComerciosApi comerciosApi, IFlotaApi flotaApi)
    {
        _usuariosApi = usuariosApi;
        _comerciosApi = comerciosApi;
        _flotaApi = flotaApi;
    }

    [BindProperty]
    public CrearUsuarioDto NuevoUsuario { get; set; } = new();

    public IReadOnlyList<PerfilDto> Perfiles { get; set; } = Array.Empty<PerfilDto>();
    public IReadOnlyList<ComercioDto> Comercios { get; set; } = Array.Empty<ComercioDto>();
    public IReadOnlyList<VehiculoDto> Vehiculos { get; set; } = Array.Empty<VehiculoDto>();

    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await CargarCatalogosAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await CargarCatalogosAsync();

        var perfilSeleccionado = Perfiles.FirstOrDefault(p => p.Id == NuevoUsuario.PerfilId);
        var esRepartidor = string.Equals(perfilSeleccionado?.Nombre, "Repartidor", StringComparison.OrdinalIgnoreCase);

        if (esRepartidor)
        {
            if (string.IsNullOrWhiteSpace(NuevoUsuario.Documento))
                ModelState.AddModelError("NuevoUsuario.Documento", "La cédula o documento es obligatorio para el rol Repartidor.");
            if (string.IsNullOrWhiteSpace(NuevoUsuario.Telefono))
                ModelState.AddModelError("NuevoUsuario.Telefono", "El teléfono celular es obligatorio para el rol Repartidor.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _usuariosApi.CrearUsuarioAsync(NuevoUsuario);
            TempData["MensajeExito"] = esRepartidor
                ? $"El repartidor '{NuevoUsuario.NombreCompleto}' fue dado de alta y sincronizado con el padrón de Repartidores y Vehículos."
                : $"El usuario '{NuevoUsuario.NombreCompleto}' fue dado de alta exitosamente con perfil asignado.";
            return RedirectToPage("/Comercios/Index", new { tab = "usuarios" });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al registrar usuario: {ex.Message}";
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
