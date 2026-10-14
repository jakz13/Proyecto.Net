using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Comercios;
using UltimaMilla.Frontend.Contracts.Modelos.Usuarios;

namespace UltimaMilla.Backoffice.Pages.Comercios;

/// <summary>
/// Controlador de la vista principal del caso de uso UC-01: Gestión de comercios y usuarios.
/// Permite listar, filtrar, buscar, dar de alta/baja y modificar comercios y usuarios del operador.
/// </summary>
[Authorize(Roles = "Administrador")]
public class IndexModel : PageModel
{
    private readonly IComerciosApi _comerciosApi;
    private readonly IUsuariosApi _usuariosApi;

    public IndexModel(IComerciosApi comerciosApi, IUsuariosApi usuariosApi)
    {
        _comerciosApi = comerciosApi;
        _usuariosApi = usuariosApi;
    }

    // Pestaña activa: "comercios" o "usuarios"
    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "comercios";

    // Filtros de Comercios
    [BindProperty(SupportsGet = true)]
    public string? BusquedaComercio { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? EstadoComercio { get; set; } = "todos";

    [BindProperty(SupportsGet = true)]
    public int PaginaComercio { get; set; } = 1;

    // Filtros de Usuarios
    [BindProperty(SupportsGet = true)]
    public string? BusquedaUsuario { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? PerfilFiltro { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? EstadoUsuario { get; set; } = "todos";

    [BindProperty(SupportsGet = true)]
    public int PaginaUsuario { get; set; } = 1;

    // Resultados
    public ResultadoPaginadoDto<ComercioDto> ResultadoComercios { get; set; } = new();
    public ResultadoPaginadoDto<UsuarioDto> ResultadoUsuarios { get; set; } = new();
    public IReadOnlyList<PerfilDto> Perfiles { get; set; } = Array.Empty<PerfilDto>();

    public string? MensajeExito { get; set; }
    public string? MensajeError { get; set; }

    public async Task OnGetAsync()
    {
        MensajeExito = TempData["MensajeExito"]?.ToString();
        MensajeError = TempData["MensajeError"]?.ToString();

        // 1. Cargar Comercios aplicando filtros
        bool? soloActivosComercio = EstadoComercio switch
        {
            "activos" => true,
            "inactivos" => false,
            _ => null
        };

        var filtroComercios = new FiltroComerciosDto
        {
            TextoBusqueda = BusquedaComercio,
            SoloActivos = soloActivosComercio,
            Pagina = Math.Max(1, PaginaComercio),
            TamanoPagina = 8
        };

        ResultadoComercios = await _comerciosApi.ListarComerciosAsync(filtroComercios);

        // 2. Cargar Perfiles disponibles para el dropdown de usuarios
        Perfiles = await _usuariosApi.ListarPerfilesAsync();

        // 3. Cargar Usuarios aplicando filtros
        bool? soloActivosUsuario = EstadoUsuario switch
        {
            "activos" => true,
            "inactivos" => false,
            _ => null
        };

        var filtroUsuarios = new FiltroUsuariosDto
        {
            TextoBusqueda = BusquedaUsuario,
            PerfilId = PerfilFiltro,
            SoloActivos = soloActivosUsuario,
            Pagina = Math.Max(1, PaginaUsuario),
            TamanoPagina = 8
        };

        ResultadoUsuarios = await _usuariosApi.ListarUsuariosAsync(filtroUsuarios);
    }

    /// <summary>
    /// Acción directa para dar de baja lógica o reactivar un comercio con reflejo inmediato.
    /// </summary>
    public async Task<IActionResult> OnPostCambiarEstadoComercioAsync(Guid id, bool nuevoEstado)
    {
        try
        {
            await _comerciosApi.CambiarEstadoComercioAsync(id, nuevoEstado);
            TempData["MensajeExito"] = nuevoEstado
                ? "El comercio fue reactivado correctamente."
                : "El comercio fue dado de baja lógica (inactivado) correctamente.";
        }
        catch (Exception ex)
        {
            TempData["MensajeError"] = $"Error al cambiar el estado del comercio: {ex.Message}";
        }

        return RedirectToPage(new
        {
            Tab = "comercios",
            BusquedaComercio,
            EstadoComercio,
            PaginaComercio
        });
    }

    /// <summary>
    /// Acción directa para dar de baja lógica o reactivar un usuario con reflejo inmediato.
    /// </summary>
    public async Task<IActionResult> OnPostCambiarEstadoUsuarioAsync(Guid id, bool nuevoEstado)
    {
        try
        {
            await _usuariosApi.CambiarEstadoUsuarioAsync(id, nuevoEstado);
            TempData["MensajeExito"] = nuevoEstado
                ? "El usuario fue reactivado correctamente."
                : "El usuario fue dado de baja lógica (inactivado) correctamente.";
        }
        catch (Exception ex)
        {
            TempData["MensajeError"] = $"Error al cambiar el estado del usuario: {ex.Message}";
        }

        return RedirectToPage(new
        {
            Tab = "usuarios",
            BusquedaUsuario,
            PerfilFiltro,
            EstadoUsuario,
            PaginaUsuario
        });
    }
}
