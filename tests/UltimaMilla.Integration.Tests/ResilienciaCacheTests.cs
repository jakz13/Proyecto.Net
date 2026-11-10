using Microsoft.Extensions.Caching.Distributed;
using UltimaMilla.Infrastructure.Services;

namespace UltimaMilla.Integration.Tests;

/// <summary>Resiliencia del caché (opcional 7.4): con Redis caído la aplicación sigue respondiendo y no espera el tiempo de espera en cada solicitud.</summary>
public class ResilienciaCacheTests
{
    private sealed class CacheCaido : IDistributedCache
    {
        public int Llamadas;

        private Exception Falla() { Interlocked.Increment(ref Llamadas); return new InvalidOperationException("Redis no responde"); }

        public byte[]? Get(string key) => throw Falla();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Falla();
        public void Refresh(string key) => throw Falla();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Falla();
        public void Remove(string key) => throw Falla();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Falla();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Falla();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Falla();
    }

    [Fact]
    public async Task ConRedisCaido_LaLecturaDevuelveFalloDeCacheSinLanzarExcepcion()
    {
        var servicio = new DistributedCacheService(new CacheCaido());

        var valor = await servicio.GetAsync<string>("seguimiento_publico:XYZ");

        Assert.Null(valor);
    }

    [Fact]
    public async Task TrasUnFallo_NoSeVuelveAConsultarRedisDuranteLaPausa_YSeReintentaDespues()
    {
        var cache = new CacheCaido();
        var servicio = new DistributedCacheService(cache, cortacircuitos: new CortacircuitosCache(TimeSpan.FromMilliseconds(300)));

        await servicio.GetAsync<string>("a");
        await servicio.GetAsync<string>("b");
        await servicio.SetAsync("c", "x");
        await servicio.RemoveAsync("d");
        Assert.Equal(1, cache.Llamadas);   // el primer fallo abrió el cortacircuitos: las demás operaciones ni lo intentaron

        await Task.Delay(400);
        await servicio.GetAsync<string>("e");
        Assert.Equal(2, cache.Llamadas);   // pasada la pausa se vuelve a probar (recuperación automática)
    }
}
