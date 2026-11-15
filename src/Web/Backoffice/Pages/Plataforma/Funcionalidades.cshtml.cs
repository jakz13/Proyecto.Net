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
/// Con muchos operadores se busca por nombre y se filtra por funcionalidad y estado.
/// </summary>
[Authorize(Roles = RolPlataforma)]
public class FuncionalidadesModel : PageModel
{
    public const string RolPlataforma = "AdministradorPlataforma";

    private readonly IFuncionalidadesApi _api;

    public FuncionalidadesModel(IFuncionalidadesApi api) => _api = api;

    public IReadOnlyList<OperadorFuncionalidadesDto> Operadores { get; private set; } = Array.Empty<OperadorFuncionalidadesDto>();

    /// <summary>Cantidad de operadores de la plataforma antes de filtrar.</summary>
    public int TotalOperadores { get; private set; }

    /// <summary>Funcionalidades del catálogo (columnas de la tabla).</summary>
    public IReadOnlyList<(string Clave, string Nombre)> Catalogo { get; private set; } = Array.Empty<(string, string)>();

    /// <summary>Texto a buscar en el nombre del operador.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    /// <summary>Clave de la funcionalidad por la que se filtra el estado.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Funcionalidad { get; set; }

    /// <summary>"habilitada" o "deshabilitada": se aplica a la funcionalidad elegida (o a cualquiera si no se eligió ninguna).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Estado { get; set; }

    /// <summary>Solo al dar de alta una empresa (cambiar una bandera no muestra confirmación).</summary>
    [TempData]
    public string? MensajeEmpresaCreada { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnPostCambiarAsync(Guid operadorId, string clave, bool habilitada, string? q, string? funcionalidad, string? estado)
    {
        try
        {
            await _api.CambiarAsync(operadorId, clave, habilitada);
        }
        catch (ApiExcepcion ex)
        {
            MensajeError = ex.Message;
        }

        // Se conservan la búsqueda y los filtros para seguir cambiando operadores sin volver a buscar
        return RedirectToPage(new { q, funcionalidad, estado });
    }

    private async Task CargarAsync()
    {
        try
        {
            var todos = await _api.ListarPorOperadorAsync();
            TotalOperadores = todos.Count;
            Catalogo = todos.FirstOrDefault()?.Funcionalidades.Select(f => (f.Clave, f.Nombre)).ToList() ?? new List<(string, string)>();
            Operadores = Filtrar(todos).ToList();
        }
        catch (ApiExcepcion ex)
        {
            MensajeError = ex.Message;
        }
    }

    private IEnumerable<OperadorFuncionalidadesDto> Filtrar(IReadOnlyList<OperadorFuncionalidadesDto> operadores)
    {
        var texto = Q?.Trim();
        var resultado = operadores.AsEnumerable();

        if (!string.IsNullOrEmpty(texto))
            resultado = resultado.Where(o => o.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase));

        bool? habilitada = Estado switch { "habilitada" => true, "deshabilitada" => false, _ => null };
        if (habilitada is { } valor)
        {
            resultado = resultado.Where(o => o.Funcionalidades.Any(f =>
                (string.IsNullOrEmpty(Funcionalidad) || f.Clave == Funcionalidad) && f.Habilitada == valor));
        }

        return resultado;
    }
}
