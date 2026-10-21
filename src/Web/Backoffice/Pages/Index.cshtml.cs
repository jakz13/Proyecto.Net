using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Envios;
using UltimaMilla.Frontend.Contracts.Modelos.Usuarios;

namespace UltimaMilla.Backoffice.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IEnviosApi _enviosApi;
    private readonly IComerciosApi _comerciosApi;
    private readonly IUsuariosApi _usuariosApi;

    public IndexModel(IEnviosApi enviosApi, IComerciosApi comerciosApi, IUsuariosApi usuariosApi)
    {
        _enviosApi = enviosApi;
        _comerciosApi = comerciosApi;
        _usuariosApi = usuariosApi;
    }

    public int TotalComercios { get; set; }
    public int TotalUsuarios { get; set; }
    public int TotalEnvios { get; set; }
    public IReadOnlyList<EnvioDto> EnviosRecientes { get; set; } = Array.Empty<EnvioDto>();

    public async Task OnGetAsync()
    {
        try
        {
            var comercios = await _comerciosApi.ListarComerciosAsync(pagina: 1, tamanoPagina: 1);
            TotalComercios = comercios.TotalRegistros;
        }
        catch
        {
            TotalComercios = 0;
        }

        try
        {
            var usuarios = await _usuariosApi.ListarUsuariosAsync(new FiltroUsuariosDto { Pagina = 1, TamanoPagina = 1 });
            TotalUsuarios = usuarios.TotalRegistros;
        }
        catch
        {
            TotalUsuarios = 0;
        }

        try
        {
            var envios = await _enviosApi.ListarEnviosAsync(new FiltroEnviosDto { Pagina = 1, TamanoPagina = 5 });
            TotalEnvios = envios.TotalRegistros;
            EnviosRecientes = envios.Elementos;
        }
        catch
        {
            TotalEnvios = 0;
            EnviosRecientes = Array.Empty<EnvioDto>();
        }
    }
}
