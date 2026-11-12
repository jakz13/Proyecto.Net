using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Infrastructure.Services;

/// <summary>
/// Adaptador de caché distribuido sobre <see cref="IDistributedCache"/> (Redis en despliegue).
/// Instrumenta aciertos, fallos e invalidaciones por caso de uso y degrada de forma segura hacia la base de datos
/// cuando el almacén de caché no se encuentra disponible.
/// </summary>
public class DistributedCacheService : IDistributedCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedCacheService>? _logger;
    private readonly IMetricasNegocio? _metricas;

    private readonly CortacircuitosCache _cortacircuitos;

    private bool EnPausa => _cortacircuitos.EnPausa;

    private void Pausar() => _cortacircuitos.Pausar();

    public DistributedCacheService(
        IDistributedCache cache,
        ILogger<DistributedCacheService>? logger = null,
        IMetricasNegocio? metricas = null,
        CortacircuitosCache? cortacircuitos = null)
    {
        _cortacircuitos = cortacircuitos ?? new CortacircuitosCache();
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
        _logger = logger;
        _metricas = metricas;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return default;

        if (EnPausa)
        {
            MedidorCache.RegistrarFallo(key);
            _metricas?.RegistrarFalloCache();
            return default;
        }

        try
        {
            var data = await _cache.GetStringAsync(key, cancellationToken);
            if (string.IsNullOrEmpty(data))
            {
                MedidorCache.RegistrarFallo(key);
                _metricas?.RegistrarFalloCache();
                return default;
            }

            var resultado = JsonSerializer.Deserialize<T>(data);
            if (resultado is null)
            {
                MedidorCache.RegistrarFallo(key);
                _metricas?.RegistrarFalloCache();
                return default;
            }

            MedidorCache.RegistrarAcierto(key);
            _metricas?.RegistrarAciertoCache();
            return resultado;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Pausar();
            MedidorCache.RegistrarError(key);
            MedidorCache.RegistrarFallo(key);
            _metricas?.RegistrarFalloCache();
            _logger?.LogWarning(
                ex,
                "Almacén de caché distribuido no disponible al consultar el caso de uso {CasoDeUso}. Se continúa contra la base de datos.",
                ClavesCache.DeterminarCasoDeUso(key));
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || value is null || EnPausa)
            return;

        try
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Pausar();
            MedidorCache.RegistrarError(key);
            _logger?.LogWarning(
                ex,
                "Almacén de caché distribuido no disponible al almacenar entrada del caso de uso {CasoDeUso}.",
                ClavesCache.DeterminarCasoDeUso(key));
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || EnPausa)
            return;

        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
            MedidorCache.RegistrarInvalidacion(key);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Pausar();
            MedidorCache.RegistrarError(key);
            _logger?.LogWarning(
                ex,
                "Almacén de caché distribuido no disponible al invalidar entrada del caso de uso {CasoDeUso}.",
                ClavesCache.DeterminarCasoDeUso(key));
        }
    }
}
