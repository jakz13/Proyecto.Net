using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Seguimiento;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Implementación basada en HTTP/REST de ISeguimientoApi para la consulta pública y anónima de envíos (UC-33).
/// Se conecta con el endpoint del backend /api/seguimiento/{codigoSeguimiento} cumpliendo con:
/// - Anti-enumeración: ante código no existente retorna null (mapeando HTTP 404 a null).
/// - Control de tasa de peticiones: mapea HTTP 429 a ApiExcepcion con EsLimiteDeTasa = true.
/// - Resiliencia y sanitización: valida longitudes y caracteres antes de consultar.
/// </summary>
public class HttpSeguimientoApi : ISeguimientoApi
{
    private readonly HttpClient _clienteHttp;

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public HttpSeguimientoApi(HttpClient clienteHttp)
    {
        _clienteHttp = clienteHttp ?? throw new ArgumentNullException(nameof(clienteHttp));
    }

    /// <summary>
    /// Consulta el estado y la línea de tiempo pública del envío mediante su código.
    /// Si el código no existe o no tiene formato válido, retorna null para evitar la enumeración.
    /// </summary>
    public async Task<SeguimientoPublicoDto?> ConsultarSeguimientoAsync(string codigoSeguimiento, CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(codigoSeguimiento))
        {
            return null;
        }

        var codigoLimpio = codigoSeguimiento.Trim().ToUpperInvariant();

        // Limite de seguridad para evitar peticiones anómalas
        if (codigoLimpio.Length > 100)
        {
            return null;
        }

        var rutaUrl = $"/api/seguimiento/{Uri.EscapeDataString(codigoLimpio)}";
        HttpResponseMessage respuesta;

        try
        {
            respuesta = await _clienteHttp.GetAsync(rutaUrl, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(
                HttpStatusCode.ServiceUnavailable,
                $"No fue posible comunicarse con el servicio de seguimiento: {ex.Message}"
            );
        }

        // Caso 404: regla obligatoria de anti-enumeración (UC-33)
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        // Caso 429: límite de consultas por IP excedido
        if (respuesta.StatusCode == (HttpStatusCode)429)
        {
            throw new ApiExcepcion(
                (HttpStatusCode)429,
                "Demasiadas consultas en poco tiempo. Por favor aguarde unos instantes antes de reintentar."
            );
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var cuerpoError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(
                respuesta.StatusCode,
                string.IsNullOrWhiteSpace(cuerpoError) ? $"Error HTTP {respuesta.StatusCode}" : cuerpoError
            );
        }

        try
        {
            var resultado = await respuesta.Content.ReadFromJsonAsync<SeguimientoPublicoDto>(OpcionesJson, cancelacion);
            return resultado;
        }
        catch (JsonException ex)
        {
            throw new ApiExcepcion(
                HttpStatusCode.InternalServerError,
                $"Error al procesar la respuesta del servidor de seguimiento: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Envía la solicitud de reprogramación de entrega iniciada por el destinatario (UC-34).
    /// </summary>
    public async Task<bool> SolicitarReprogramacionAsync(string codigoSeguimiento, DateTime nuevaFecha, CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(codigoSeguimiento))
        {
            return false;
        }

        var codigoLimpio = codigoSeguimiento.Trim().ToUpperInvariant();
        var rutaUrl = $"/api/seguimiento/{Uri.EscapeDataString(codigoLimpio)}/reprogramar";
        var cargaUtil = new { NuevaFecha = nuevaFecha };

        try
        {
            var respuesta = await _clienteHttp.PostAsJsonAsync(rutaUrl, cargaUtil, OpcionesJson, cancelacion);
            return respuesta.IsSuccessStatusCode;
        }
        catch
        {
            // Fallback seguro ante endpoints aún no desplegados en el backend
            return false;
        }
    }
}
