using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Infrastructure.Services;

/// <summary>
/// Geocodificador sobre Nominatim (OpenStreetMap). Es un servicio público con límite de un pedido por segundo,
/// así que los pedidos se espacian y los resultados (incluso los "no encontrada") se recuerdan un tiempo.
/// La caché es solo una optimización: no guarda estado de negocio y cada instancia puede tener la suya.
/// </summary>
public class NominatimGeocodificador : IGeocodificador
{
    private static readonly TimeSpan EsperaEntrePedidos = TimeSpan.FromMilliseconds(1100);
    private static readonly SemaphoreSlim Turno = new(1, 1);
    private static DateTime _ultimoPedidoUtc = DateTime.MinValue;

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly string _codigoPais;

    public NominatimGeocodificador(HttpClient http, IMemoryCache cache, string codigoPais = "uy")
    {
        _http = http;
        _cache = cache;
        _codigoPais = codigoPais;
    }

    public async Task<(double Latitud, double Longitud)?> GeocodificarAsync(
        string calle, string numero, string ciudad, CancellationToken cancellationToken = default)
    {
        var consulta = $"{calle} {numero}, {ciudad}".Trim();
        var clave = "geocod:" + consulta.ToLowerInvariant();
        if (_cache.TryGetValue(clave, out (double, double)? enCache))
            return enCache;

        await Turno.WaitAsync(cancellationToken);
        try
        {
            // Otro pedido concurrente pudo resolver la misma dirección mientras se esperaba el turno
            if (_cache.TryGetValue(clave, out enCache))
                return enCache;

            var faltante = EsperaEntrePedidos - (DateTime.UtcNow - _ultimoPedidoUtc);
            if (faltante > TimeSpan.Zero)
                await Task.Delay(faltante, cancellationToken);

            var url = $"search?format=jsonv2&limit=1&countrycodes={Uri.EscapeDataString(_codigoPais)}&q={Uri.EscapeDataString(consulta)}";
            _ultimoPedidoUtc = DateTime.UtcNow;
            var resultados = await _http.GetFromJsonAsync<List<ResultadoNominatim>>(url, cancellationToken)
                ?? new List<ResultadoNominatim>();

            (double, double)? ubicada = null;
            var primero = resultados.FirstOrDefault();
            if (primero is not null
                && double.TryParse(primero.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                && double.TryParse(primero.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
            {
                ubicada = (lat, lon);
            }

            _cache.Set(clave, ubicada, ubicada is null ? TimeSpan.FromMinutes(10) : TimeSpan.FromDays(7));
            return ubicada;
        }
        finally
        {
            Turno.Release();
        }
    }

    private sealed class ResultadoNominatim
    {
        [JsonPropertyName("lat")] public string? Lat { get; set; }
        [JsonPropertyName("lon")] public string? Lon { get; set; }
    }
}
