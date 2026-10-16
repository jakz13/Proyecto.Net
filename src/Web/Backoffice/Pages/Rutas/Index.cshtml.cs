using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Contracts.Modelos.Rutas;

namespace UltimaMilla.Backoffice.Pages.Rutas;

[Authorize(Roles = "Despachador,Administrador")]
public class IndexModel : PageModel
{
    private readonly IRutasApi _rutasApi;
    private readonly IFlotaApi _flotaApi;

    public IndexModel(IRutasApi rutasApi, IFlotaApi flotaApi)
    {
        _rutasApi = rutasApi;
        _flotaApi = flotaApi;
    }

    [BindProperty(SupportsGet = true)]
    public string? FechaFiltro { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? RepartidorFiltro { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? EstadoFiltro { get; set; } = "todos";

    [BindProperty(SupportsGet = true)]
    public string? Busqueda { get; set; }

    public IReadOnlyList<HojaDeRutaDto> HojasDeRuta { get; private set; } = new List<HojaDeRutaDto>();
    public IReadOnlyList<RepartidorDto> Repartidores { get; private set; } = new List<RepartidorDto>();
    public int EnviosPendientesCount { get; private set; }

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    public async Task OnGetAsync()
    {
        DateOnly? fecha = null;
        if (!string.IsNullOrWhiteSpace(FechaFiltro) && DateOnly.TryParse(FechaFiltro, out var fechaParseada))
        {
            fecha = fechaParseada;
        }

        var filtro = new FiltroRutasDto
        {
            Fecha = fecha,
            RepartidorId = RepartidorFiltro,
            Estado = EstadoFiltro,
            TerminoBusqueda = Busqueda
        };

        HojasDeRuta = await _rutasApi.ListarHojasDeRutaAsync(filtro);
        Repartidores = await _flotaApi.ListarRepartidoresAsync();

        var pendientes = await _rutasApi.ListarEnviosPendientesAsync();
        EnviosPendientesCount = pendientes.Count;
    }
}
