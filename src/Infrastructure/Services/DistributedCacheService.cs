using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Infrastructure.Services;

public class DistributedCacheService : IDistributedCacheService
{
    private readonly IDistributedCache _cache;
    private readonly IMetricasNegocio? _metricas;

    public DistributedCacheService(IDistributedCache cache, IMetricasNegocio? metricas = null)
    {
        _cache = cache;
        _metricas = metricas;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var data = await _cache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrEmpty(data))
        {
            // Registra fallo (miss) en las métricas de observabilidad (UC-44)
            _metricas?.RegistrarFalloCache();
            return default;
        }

        // Registra acierto (hit) en las métricas de observabilidad (UC-44)
        _metricas?.RegistrarAciertoCache();
        return JsonSerializer.Deserialize<T>(data);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions();
        if (expiration.HasValue)
        {
            options.SetAbsoluteExpiration(expiration.Value);
        }
        else
        {
            options.SetSlidingExpiration(TimeSpan.FromMinutes(30));
        }

        var json = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, json, options, cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(key, cancellationToken);
    }
}
