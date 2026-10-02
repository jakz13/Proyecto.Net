using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using UltimaMilla.Mobile.Configuration;
using UltimaMilla.Mobile.Models.DTOs;

namespace UltimaMilla.Mobile.Services.Api;

public interface IApiClient
{
    Task<MobileConnectivityDto?> CheckConnectivityAsync(CancellationToken cancellationToken = default);
}

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<MobileConnectivityDto?> CheckConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Enviando ping de conectividad a {Url}...", ApiEndpoints.MobilePing);
            var response = await _httpClient.GetFromJsonAsync<MobileConnectivityDto>(
                ApiEndpoints.MobilePing,
                cancellationToken
            );
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al comunicarse con la Web API en {Url}", ApiEndpoints.MobilePing);
            throw;
        }
    }
}
