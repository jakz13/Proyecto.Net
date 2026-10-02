using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UltimaMilla.Backoffice.Pages;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public List<EnvioViewModel> Envios { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public bool IsApiReachable { get; set; } = true;

    public async Task OnGetAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("UltimaMillaApi");
            var result = await client.GetFromJsonAsync<List<EnvioViewModel>>("/api/envios");

            if (result != null)
            {
                Envios = result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar los envíos desde la API.");
            ErrorMessage = $"No se pudo obtener el listado de envíos: {ex.Message}";
            IsApiReachable = false;
        }
    }

    public record EnvioViewModel(
        Guid Id,
        Guid ComercioId,
        string DireccionDestino,
        string NombreDestinatario,
        DateTime FechaCreacion,
        string Estado
    );
}
