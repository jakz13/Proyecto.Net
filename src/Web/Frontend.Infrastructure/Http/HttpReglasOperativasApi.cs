using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.ReglasOperativas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Implementación basada en cliente HTTP REST de IReglasOperativasApi (UC-04, RF-04).
/// </summary>
public class HttpReglasOperativasApi : IReglasOperativasApi
{
    private readonly HttpClient _clienteHttp;
    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    public HttpReglasOperativasApi(HttpClient clienteHttp)
    {
        _clienteHttp = clienteHttp;
    }

    public async Task<IReadOnlyList<ReglaOperativaDto>> ListarVersionesAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.GetAsync("/api/reglas-operativas", cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var lista = await respuesta.Content.ReadFromJsonAsync<List<ReglaOperativaDto>>(OpcionesJson, cancelacion);
        return lista ?? (IReadOnlyList<ReglaOperativaDto>)Array.Empty<ReglaOperativaDto>();
    }

    public async Task<ReglaOperativaDto?> ObtenerVersionVigenteAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.GetAsync("/api/reglas-operativas/vigente", cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<ReglaOperativaDto>(OpcionesJson, cancelacion);
    }

    public async Task<ReglaOperativaDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.GetAsync($"/api/reglas-operativas/{id}", cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<ReglaOperativaDto>(OpcionesJson, cancelacion);
    }

    public async Task<ReglaOperativaDto> PublicarNuevaVersionAsync(CrearReglaOperativaDto nuevaVersion, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PostAsJsonAsync("/api/reglas-operativas", nuevaVersion, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var creado = await respuesta.Content.ReadFromJsonAsync<ReglaOperativaDto>(OpcionesJson, cancelacion);
        return creado ?? throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo deserializar la regla operativa creada.");
    }

    public async Task<ResultadoSimulacionReglasDto> SimularReglasAsync(SimularReglaOperativaDto datosEnvio, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PostAsJsonAsync("/api/reglas-operativas/simular", datosEnvio, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoSimulacionReglasDto>(OpcionesJson, cancelacion);
        return resultado ?? new ResultadoSimulacionReglasDto { Exitoso = false, AccionSugerida = "Respuesta vacía del simulador de reglas operativas." };
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
