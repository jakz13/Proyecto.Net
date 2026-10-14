using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Tarifas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Implementación basada en HTTP/REST de ICuadroTarifarioApi.
/// Conecta el Backoffice con los endpoints de cuadros tarifarios y versionado del backend (UC-03, RF-03).
/// </summary>
public class HttpCuadroTarifarioApi : ICuadroTarifarioApi
{
    private readonly HttpClient _clienteHttp;
    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    public HttpCuadroTarifarioApi(HttpClient clienteHttp)
    {
        _clienteHttp = clienteHttp;
    }

    public async Task<IReadOnlyList<CuadroTarifarioDto>> ListarVersionesAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.GetAsync("/api/cuadros-tarifarios", cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var lista = await respuesta.Content.ReadFromJsonAsync<List<CuadroTarifarioDto>>(OpcionesJson, cancelacion);
        return lista ?? (IReadOnlyList<CuadroTarifarioDto>)Array.Empty<CuadroTarifarioDto>();
    }

    public async Task<CuadroTarifarioDto?> ObtenerVersionVigenteAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.GetAsync("/api/cuadros-tarifarios/vigente", cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<CuadroTarifarioDto>(OpcionesJson, cancelacion);
    }

    public async Task<CuadroTarifarioDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.GetAsync($"/api/cuadros-tarifarios/{id}", cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<CuadroTarifarioDto>(OpcionesJson, cancelacion);
    }

    public async Task<CuadroTarifarioDto> PublicarNuevaVersionAsync(CrearVersionCuadroTarifarioDto nuevaVersion, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PostAsJsonAsync("/api/cuadros-tarifarios", nuevaVersion, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var creado = await respuesta.Content.ReadFromJsonAsync<CuadroTarifarioDto>(OpcionesJson, cancelacion);
        return creado ?? throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo deserializar el cuadro tarifario creado.");
    }

    public async Task<CotizacionSimuladaDto> SimularTarifaAsync(SimularTarifaDto datosEnvio, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PostAsJsonAsync("/api/cuadros-tarifarios/simular", datosEnvio, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var resultado = await respuesta.Content.ReadFromJsonAsync<CotizacionSimuladaDto>(OpcionesJson, cancelacion);
        return resultado ?? new CotizacionSimuladaDto { Exitoso = false, Mensaje = "Respuesta vacía del simulador de tarifas." };
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
