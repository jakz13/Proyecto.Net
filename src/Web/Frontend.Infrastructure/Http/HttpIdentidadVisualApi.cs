using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Implementación basada en cliente HTTP REST de IIdentidadVisualApi (UC-06, RF-05).
/// Conecta la administración de identidad visual con los endpoints del backend.
/// </summary>
public class HttpIdentidadVisualApi : IIdentidadVisualApi
{
    private readonly HttpClient _clienteHttp;
    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    public HttpIdentidadVisualApi(HttpClient clienteHttp)
    {
        _clienteHttp = clienteHttp;
    }

    public async Task<IdentidadVisualDto> ObtenerIdentidadActualAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.GetAsync("/api/v1/identidad-visual", cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var identidad = await respuesta.Content.ReadFromJsonAsync<IdentidadVisualDto>(OpcionesJson, cancelacion);
        return identidad ?? new IdentidadVisualDto();
    }

    public async Task<IdentidadVisualDto> ObtenerPorOperadorDemoAsync(string operadorId, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.GetAsync($"/api/v1/identidad-visual/operadores-demo/{operadorId}", cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var identidad = await respuesta.Content.ReadFromJsonAsync<IdentidadVisualDto>(OpcionesJson, cancelacion);
        return identidad ?? new IdentidadVisualDto();
    }

    public async Task<IReadOnlyList<IdentidadVisualDto>> ListarOperadoresDemoAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.GetAsync("/api/v1/identidad-visual/operadores-demo", cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var lista = await respuesta.Content.ReadFromJsonAsync<List<IdentidadVisualDto>>(OpcionesJson, cancelacion);
        return lista ?? (IReadOnlyList<IdentidadVisualDto>)Array.Empty<IdentidadVisualDto>();
    }

    public async Task<IdentidadVisualDto> ActualizarIdentidadVisualAsync(ActualizarIdentidadVisualDto dto, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PutAsJsonAsync("/api/v1/identidad-visual", dto, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var actualizada = await respuesta.Content.ReadFromJsonAsync<IdentidadVisualDto>(OpcionesJson, cancelacion);
        return actualizada ?? throw new ApiExcepcion(System.Net.HttpStatusCode.InternalServerError, "No se pudo deserializar la identidad visual actualizada.");
    }

    private static async Task AsegurarRespuestaExitosaAsync(HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        if (respuesta.IsSuccessStatusCode)
        {
            return;
        }

        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);
        throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(cuerpo) ? $"Error HTTP {respuesta.StatusCode}" : cuerpo);
    }
}
