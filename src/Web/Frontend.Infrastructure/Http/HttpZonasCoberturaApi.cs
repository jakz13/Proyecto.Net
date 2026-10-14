using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Implementación basada en HTTP/REST de IZonasCoberturaApi.
/// Conecta el Backoffice con los endpoints de zonas de cobertura y franjas horarias del backend (UC-02, RF-02).
/// </summary>
public class HttpZonasCoberturaApi : IZonasCoberturaApi
{
    private readonly HttpClient _clienteHttp;
    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    public HttpZonasCoberturaApi(HttpClient clienteHttp)
    {
        _clienteHttp = clienteHttp;
    }

    public async Task<IReadOnlyList<ZonaCoberturaDto>> ListarZonasAsync(FiltroZonasDto? filtro = null, CancellationToken cancelacion = default)
    {
        var url = "/api/zonas-cobertura?";
        if (filtro != null)
        {
            if (!string.IsNullOrWhiteSpace(filtro.TextoBusqueda))
            {
                url += $"q={Uri.EscapeDataString(filtro.TextoBusqueda)}&";
            }
            if (filtro.SoloActivas.HasValue)
            {
                url += $"soloActivas={filtro.SoloActivas.Value}&";
            }
        }

        var respuesta = await _clienteHttp.GetAsync(url.TrimEnd('?', '&'), cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var zonas = await respuesta.Content.ReadFromJsonAsync<List<ZonaCoberturaDto>>(OpcionesJson, cancelacion);
        return zonas ?? (IReadOnlyList<ZonaCoberturaDto>)Array.Empty<ZonaCoberturaDto>();
    }

    public async Task<ZonaCoberturaDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.GetAsync($"/api/zonas-cobertura/{id}", cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<ZonaCoberturaDto>(OpcionesJson, cancelacion);
    }

    public async Task<ZonaCoberturaDto> CrearZonaAsync(CrearZonaCoberturaDto nuevaZona, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PostAsJsonAsync("/api/zonas-cobertura", nuevaZona, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var creada = await respuesta.Content.ReadFromJsonAsync<ZonaCoberturaDto>(OpcionesJson, cancelacion);
        return creada ?? throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo deserializar la zona creada.");
    }

    public async Task<ZonaCoberturaDto> ActualizarZonaAsync(Guid id, ActualizarZonaCoberturaDto zonaActualizada, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PutAsJsonAsync($"/api/zonas-cobertura/{id}", zonaActualizada, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var actualizada = await respuesta.Content.ReadFromJsonAsync<ZonaCoberturaDto>(OpcionesJson, cancelacion);
        return actualizada ?? throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo deserializar la zona actualizada.");
    }

    public async Task CambiarEstadoZonaAsync(Guid id, bool activa, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PatchAsJsonAsync($"/api/zonas-cobertura/{id}/estado", new { Activa = activa }, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);
    }

    public async Task<(bool EsValido, string? MensajeError)> ValidarSuperposicionFranjasAsync(
        IEnumerable<CrearFranjaHorariaDto> franjas,
        CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PostAsJsonAsync("/api/zonas-cobertura/validar-franjas", franjas, cancelacion);
        if (respuesta.IsSuccessStatusCode)
        {
            return (true, null);
        }

        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);
        return (false, string.IsNullOrWhiteSpace(cuerpo) ? "Superposición horaria inconsistente detectada." : cuerpo);
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
