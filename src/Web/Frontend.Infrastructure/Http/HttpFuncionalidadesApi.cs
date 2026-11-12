using System.Net.Http.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Funcionalidades;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>Cliente HTTP de las banderas de funcionalidad por operador.</summary>
public class HttpFuncionalidadesApi : IFuncionalidadesApi
{
    private readonly HttpClient _http;

    public HttpFuncionalidadesApi(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<OperadorFuncionalidadesDto>> ListarPorOperadorAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _http.GetAsync("api/plataforma/operadores", cancelacion);
        if (!respuesta.IsSuccessStatusCode)
            throw new ApiExcepcion(respuesta.StatusCode, $"No se pudieron consultar las funcionalidades ({(int)respuesta.StatusCode}).");

        var lista = await respuesta.Content.ReadFromJsonAsync<List<OperadorFuncionalidadesDto>>(cancellationToken: cancelacion);
        return lista ?? new List<OperadorFuncionalidadesDto>();
    }

    public async Task CambiarAsync(Guid operadorId, string clave, bool habilitada, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.PutAsJsonAsync($"api/plataforma/operadores/{operadorId}/funcionalidades/{Uri.EscapeDataString(clave)}", new { habilitada }, cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, $"No se pudo cambiar la funcionalidad: {detalle}");
        }
    }
}
