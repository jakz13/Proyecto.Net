using System.Net.Http.Json;

namespace UltimaMilla.PortalComercio.Client.Services;

public class EnvioApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EnvioApiClient> _logger;

    public EnvioApiClient(HttpClient httpClient, ILogger<EnvioApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(bool Success, Guid? Id, string? ErrorMessage)> CrearEnvioAsync(string direccionDestino, string nombreDestinatario)
    {
        try
        {
            var payload = new
            {
                ComercioId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                DireccionDestino = direccionDestino,
                NombreDestinatario = nombreDestinatario
            };

            var response = await _httpClient.PostAsJsonAsync("/api/envios", payload);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CreateResponse>();
                return (true, result?.Id, null);
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            return (false, null, $"Error ({response.StatusCode}): {errorBody}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al conectar con la API de envíos");
            return (false, null, $"No se pudo comunicar con el servicio: {ex.Message}");
        }
    }

    private record CreateResponse(Guid Id);
}
