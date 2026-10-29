using System.Net;
using System.Net.Http.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Tablero;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>Cliente HTTP del tablero de operación en vivo (UC-12, RF-29).</summary>
public class HttpTableroApi : ITableroApi
{
    private readonly HttpClient _http;

    public HttpTableroApi(HttpClient http)
    {
        _http = http;
    }

    public async Task<ResumenTableroDto> ObtenerResumenEnVivoAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _http.GetAsync("api/tablero/resumen", cancelacion);
        await AsegurarExitoAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<ResumenTableroDto>(cancellationToken: cancelacion)
            ?? throw new ApiExcepcion("La API devolvió un tablero vacío.");
    }

    public async Task<VehiculoEnVivoDto?> ObtenerDetalleVehiculoEnVivoAsync(Guid hojaDeRutaId, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.GetAsync($"api/tablero/hojas-de-ruta/{hojaDeRutaId}", cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return null;

        await AsegurarExitoAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<VehiculoEnVivoDto>(cancellationToken: cancelacion);
    }

    public async Task<bool> EnviarInstruccionRepartidorAsync(Guid repartidorId, string mensaje, CancellationToken cancelacion = default)
    {
        if (repartidorId == Guid.Empty || string.IsNullOrWhiteSpace(mensaje))
            return false;

        // La instrucción la registra el módulo de tablero de operación, que la publica para que el worker la entregue al dispositivo
        var respuesta = await _http.PostAsJsonAsync("api/operacion/tablero/instrucciones", new { repartidorId, mensaje = mensaje.Trim() }, cancelacion);
        if (respuesta.IsSuccessStatusCode)
            return true;

        if (respuesta.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
            return false;

        await AsegurarExitoAsync(respuesta, cancelacion);
        return false;
    }

    private static async Task AsegurarExitoAsync(HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        if (respuesta.IsSuccessStatusCode)
            return;

        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);
        throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(cuerpo) ? $"Error HTTP {(int)respuesta.StatusCode}" : cuerpo);
    }
}
