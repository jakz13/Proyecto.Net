using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Liquidaciones;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>Implementación HTTP de ICuentaCorrienteApi (RF-34).</summary>
public class HttpCuentaCorrienteApi : ICuentaCorrienteApi
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    public HttpCuentaCorrienteApi(HttpClient http) => _http = http;

    public async Task<CuentaCorrienteDto> ObtenerCuentaCorrienteAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _http.GetAsync("/api/comercio/cuenta-corriente", cancelacion);
        await AsegurarAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<CuentaCorrienteDto>(OpcionesJson, cancelacion) ?? new CuentaCorrienteDto();
    }

    public async Task<IReadOnlyList<LiquidacionDto>> ListarLiquidacionesAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _http.GetAsync("/api/comercio/liquidaciones", cancelacion);
        await AsegurarAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<List<LiquidacionDto>>(OpcionesJson, cancelacion) ?? new();
    }

    private static async Task AsegurarAsync(HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        if (respuesta.IsSuccessStatusCode) return;
        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);
        throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(cuerpo) ? $"Error HTTP {(int)respuesta.StatusCode}" : cuerpo);
    }
}
