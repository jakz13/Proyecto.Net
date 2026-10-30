using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Avisos;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Cliente HTTP para la configuración, monitoreo y reintento de avisos automáticos y webhooks (UC-29, UC-30).
/// </summary>
public class HttpAvisosComercioApi : IAvisosComercioApi
{
    private readonly HttpClient _clienteHttp;
    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    public HttpAvisosComercioApi(HttpClient clienteHttp)
    {
        _clienteHttp = clienteHttp;
    }

    public async Task<ConfiguracionAvisosDto> ObtenerConfiguracionAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.GetAsync("/api/comercio/avisos", cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var config = await respuesta.Content.ReadFromJsonAsync<ConfiguracionAvisosDto>(OpcionesJson, cancelacion);
        return config ?? new ConfiguracionAvisosDto();
    }

    public async Task<ConfiguracionAvisosDto> GuardarConfiguracionAsync(GuardarConfiguracionAvisosDto dto, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var respuesta = await _clienteHttp.PostAsJsonAsync("/api/comercio/avisos", dto, OpcionesJson, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var respuestaGuardada = await respuesta.Content.ReadFromJsonAsync<ConfiguracionAvisosDto>(OpcionesJson, cancelacion);
        return respuestaGuardada ?? throw new ApiExcepcion(System.Net.HttpStatusCode.InternalServerError, "No se pudo interpretar la respuesta del servidor.");
    }

    public async Task<ResultadoPruebaWebhookDto> EnviarPruebaWebhookAsync(string? urlOpcional = null, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PostAsJsonAsync("/api/comercio/avisos/probar", new { UrlWebhook = urlOpcional }, OpcionesJson, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoPruebaWebhookDto>(OpcionesJson, cancelacion);
        return resultado ?? throw new ApiExcepcion(System.Net.HttpStatusCode.InternalServerError, "No se pudo interpretar el resultado de la prueba.");
    }

    public async Task<IReadOnlyList<AvisoHistorialDetalleDto>> ConsultarHistorialAvisosAsync(
        string? estado = null,
        Guid? envioId = null,
        string? codigoSeguimiento = null,
        CancellationToken cancelacion = default)
    {
        var parametros = new List<string>();
        if (!string.IsNullOrWhiteSpace(estado))
            parametros.Add($"estado={Uri.EscapeDataString(estado.Trim())}");
        if (envioId.HasValue && envioId.Value != Guid.Empty)
            parametros.Add($"envioId={envioId.Value}");
        if (!string.IsNullOrWhiteSpace(codigoSeguimiento))
            parametros.Add($"codigoSeguimiento={Uri.EscapeDataString(codigoSeguimiento.Trim())}");

        var url = "/api/comercio/avisos/historial";
        if (parametros.Count > 0)
            url += "?" + string.Join("&", parametros);

        var respuesta = await _clienteHttp.GetAsync(url, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var lista = await respuesta.Content.ReadFromJsonAsync<List<AvisoHistorialDetalleDto>>(OpcionesJson, cancelacion);
        return lista ?? new List<AvisoHistorialDetalleDto>();
    }

    public async Task<ResultadoReintentoAvisoDto> ReintentarAvisoAsync(Guid avisoId, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PostAsync($"/api/comercio/avisos/{avisoId}/reintentar", null, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoReintentoAvisoDto>(OpcionesJson, cancelacion);
        return resultado ?? throw new ApiExcepcion(System.Net.HttpStatusCode.InternalServerError, "No se pudo interpretar el resultado del reintento.");
    }

    private static async Task AsegurarRespuestaExitosaAsync(HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        if (respuesta.IsSuccessStatusCode)
            return;

        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);
        throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(cuerpo) ? $"Error HTTP {respuesta.StatusCode}" : cuerpo);
    }
}
