using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Comercios;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Implementación basada en HTTP/REST de IComerciosApi.
/// Facilita la conexión directa con el backend de microservicios o API monolítica.
/// </summary>
public class HttpComerciosApi : IComerciosApi
{
    private readonly HttpClient _clienteHttp;
    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    public HttpComerciosApi(HttpClient clienteHttp)
    {
        _clienteHttp = clienteHttp;
    }

    public async Task<ResultadoPaginadoDto<ComercioDto>> ListarComerciosAsync(FiltroComerciosDto filtro, CancellationToken cancelacion = default)
    {
        var url = $"/api/comercios?pagina={filtro.Pagina}&tamanoPagina={filtro.TamanoPagina}";
        if (!string.IsNullOrWhiteSpace(filtro.TextoBusqueda))
        {
            url += $"&q={Uri.EscapeDataString(filtro.TextoBusqueda)}";
        }
        if (filtro.SoloActivos.HasValue)
        {
            url += $"&soloActivos={filtro.SoloActivos.Value}";
        }

        var respuesta = await _clienteHttp.GetAsync(url, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoPaginadoDto<ComercioDto>>(OpcionesJson, cancelacion);
        return resultado ?? new ResultadoPaginadoDto<ComercioDto>();
    }

    public Task<ResultadoPaginadoDto<ComercioDto>> ListarComerciosAsync(int pagina = 1, int tamanoPagina = 10, CancellationToken cancelacion = default)
    {
        return ListarComerciosAsync(new FiltroComerciosDto { Pagina = pagina, TamanoPagina = tamanoPagina }, cancelacion);
    }

    public async Task<ComercioDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.GetAsync($"/api/comercios/{id}", cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<ComercioDto>(OpcionesJson, cancelacion);
    }

    public async Task<ComercioDto> CrearComercioAsync(CrearComercioDto nuevoComercio, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PostAsJsonAsync("/api/comercios", nuevoComercio, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var creado = await respuesta.Content.ReadFromJsonAsync<ComercioDto>(OpcionesJson, cancelacion);
        return creado ?? throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo deserializar el comercio creado.");
    }

    public async Task<ComercioDto> ActualizarComercioAsync(Guid id, ActualizarComercioDto comercioActualizado, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PutAsJsonAsync($"/api/comercios/{id}", comercioActualizado, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var actualizado = await respuesta.Content.ReadFromJsonAsync<ComercioDto>(OpcionesJson, cancelacion);
        return actualizado ?? throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo deserializar el comercio actualizado.");
    }

    public async Task CambiarEstadoComercioAsync(Guid id, bool activo, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PatchAsJsonAsync($"/api/comercios/{id}/estado", new { Activo = activo }, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);
    }

    private static async Task AsegurarRespuestaExitosaAsync(HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        if (respuesta.IsSuccessStatusCode)
        {
            return;
        }

        if (respuesta.StatusCode == HttpStatusCode.Conflict)
        {
            throw new ApiExcepcion(
                HttpStatusCode.Conflict,
                "Otra persona modificó este registro, recargá y reintentá."
            );
        }

        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);
        throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(cuerpo) ? $"Error HTTP {respuesta.StatusCode}" : cuerpo);
    }
}
