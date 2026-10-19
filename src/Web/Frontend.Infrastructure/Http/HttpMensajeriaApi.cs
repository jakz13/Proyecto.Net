using System.Net.Http.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Mensajeria;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Cliente HTTP para consumir los endpoints de mensajería asíncrona y cola de fallidos (UC-42).
/// </summary>
public class HttpMensajeriaApi : IMensajeriaApi
{
    private readonly HttpClient _http;

    public HttpMensajeriaApi(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<MensajeFallidoDto>> ListarMensajesFallidosAsync(
        string? tipo = null,
        string? estado = null,
        string? busqueda = null,
        CancellationToken cancelacion = default)
    {
        var parametros = new List<string>();
        if (!string.IsNullOrWhiteSpace(tipo) && tipo != "todos")
        {
            parametros.Add($"tipo={Uri.EscapeDataString(tipo)}");
        }
        if (!string.IsNullOrWhiteSpace(estado) && estado != "todos")
        {
            parametros.Add($"estado={Uri.EscapeDataString(estado)}");
        }
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            parametros.Add($"busqueda={Uri.EscapeDataString(busqueda)}");
        }

        var consulta = parametros.Count > 0 ? "?" + string.Join("&", parametros) : "";
        var ruta = $"api/v1/mensajeria/cola-fallidos{consulta}";

        var resultado = await _http.GetFromJsonAsync<List<MensajeFallidoDto>>(ruta, cancelacion);
        return resultado ?? new List<MensajeFallidoDto>();
    }

    public async Task<MensajeFallidoDto?> ObtenerMensajePorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        return await _http.GetFromJsonAsync<MensajeFallidoDto>($"api/v1/mensajeria/cola-fallidos/{id}", cancelacion);
    }

    public async Task<ResultadoReintentoDto> ReintentarMensajesAsync(
        IEnumerable<Guid> ids,
        bool forzarFallo = false,
        CancellationToken cancelacion = default)
    {
        var payload = new
        {
            Ids = ids.ToList(),
            ForzarFallo = forzarFallo
        };

        var respuesta = await _http.PostAsJsonAsync("api/v1/mensajeria/cola-fallidos/reintentar", payload, cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion($"Error al reintentar los mensajes de la cola: {detalle}");
        }

        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoReintentoDto>(cancellationToken: cancelacion);
        return resultado ?? throw new ApiExcepcion("No se pudo obtener el resultado del reintento.");
    }

    public async Task<bool> DescartarMensajeAsync(Guid id, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.DeleteAsync($"api/v1/mensajeria/cola-fallidos/{id}", cancelacion);
        return respuesta.IsSuccessStatusCode;
    }

    public async Task<int> LimpiarMensajesResueltosAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _http.PostAsync("api/v1/mensajeria/cola-fallidos/limpiar-resueltos", null, cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            return 0;
        }

        var eliminados = await respuesta.Content.ReadFromJsonAsync<int>(cancellationToken: cancelacion);
        return eliminados;
    }
}
