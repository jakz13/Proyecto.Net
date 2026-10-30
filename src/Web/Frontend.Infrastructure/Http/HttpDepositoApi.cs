using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Deposito;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Implementación HTTP de IDepositoApi para comunicación directa con los endpoints de depósito (UC-14, RF-10).
/// </summary>
public class HttpDepositoApi : IDepositoApi
{
    private readonly HttpClient _clienteHttp;

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public HttpDepositoApi(HttpClient clienteHttp)
    {
        _clienteHttp = clienteHttp ?? throw new ArgumentNullException(nameof(clienteHttp));
    }

    public async Task<IReadOnlyList<EnvioAdmitidoDto>> ListarEnviosAdmitidosAsync(CancellationToken cancelacion = default)
    {
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _clienteHttp.GetAsync("/api/deposito/envios-admitidos", cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de comunicación al consultar envíos admitidos: {ex.Message}");
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode}" : detalleError);
        }

        var lista = await respuesta.Content.ReadFromJsonAsync<List<EnvioAdmitidoDto>>(OpcionesJson, cancelacion);
        return lista ?? (IReadOnlyList<EnvioAdmitidoDto>)Array.Empty<EnvioAdmitidoDto>();
    }

    public async Task<EnvioAdmitidoDto?> ObtenerEnvioParaRecepcionAsync(Guid envioId, CancellationToken cancelacion = default)
    {
        var lista = await ListarEnviosAdmitidosAsync(cancelacion);
        var envio = lista.FirstOrDefault(e => e.EnvioId == envioId);
        if (envio != null)
        {
            return envio;
        }

        // Fallback: consultar el detalle directo del envío
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _clienteHttp.GetAsync($"/api/envios/{envioId}/detalle", cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de red al consultar detalle de envío: {ex.Message}");
        }

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode}" : detalleError);
        }

        var detalle = await respuesta.Content.ReadFromJsonAsync<DetalleEnvioRespuestaApi>(OpcionesJson, cancelacion);
        if (detalle == null) return null;

        return new EnvioAdmitidoDto(
            detalle.EnvioId,
            detalle.CodigoSeguimiento,
            detalle.NombreDestinatario,
            detalle.DireccionDestino,
            null,
            detalle.ModalidadServicio,
            detalle.Bultos?.Sum(b => b.PesoKg) ?? 0,
            detalle.Bultos?.Count ?? 0,
            detalle.FechaAlta,
            detalle.Bultos?.Select(b => new BultoResumenDto(
                b.BultoId,
                b.Identificacion,
                b.PesoKg,
                0, 0, 0,
                b.EstadoBulto)).ToList() ?? new List<BultoResumenDto>()
        );
    }

    public async Task<ResultadoRecepcionDto> RecepcionarBultosAsync(
        Guid envioId,
        RecepcionarBultosRequestDto solicitud,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var url = $"/api/deposito/envios/{envioId}/recepcion";
        HttpResponseMessage respuesta;

        try
        {
            respuesta = await _clienteHttp.PostAsJsonAsync(url, solicitud, OpcionesJson, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de comunicación al recepcionar bultos: {ex.Message}");
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            try
            {
                using var doc = JsonDocument.Parse(detalleError);
                if (doc.RootElement.TryGetProperty("error", out var prop))
                {
                    detalleError = prop.GetString() ?? detalleError;
                }
            }
            catch
            {
                // Dejar detalleError como string original
            }

            throw new ApiExcepcion(respuesta.StatusCode, detalleError);
        }

        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoRecepcionDto>(OpcionesJson, cancelacion);
        if (resultado == null)
        {
            throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo interpretar el resultado de la recepción.");
        }

        return resultado;
    }

    public async Task<IReadOnlyList<EnvioEnDepositoDto>> ListarEnviosEnDepositoAsync(CancellationToken cancelacion = default)
    {
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _clienteHttp.GetAsync("/api/deposito/envios-en-deposito", cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de comunicación al consultar envíos en depósito: {ex.Message}");
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode}" : detalleError);
        }

        var lista = await respuesta.Content.ReadFromJsonAsync<List<EnvioEnDepositoDto>>(OpcionesJson, cancelacion);
        return lista ?? (IReadOnlyList<EnvioEnDepositoDto>)Array.Empty<EnvioEnDepositoDto>();
    }

    public async Task<EnvioEnDepositoDto?> ObtenerEnvioParaClasificacionAsync(Guid envioId, CancellationToken cancelacion = default)
    {
        var lista = await ListarEnviosEnDepositoAsync(cancelacion);
        var envio = lista.FirstOrDefault(e => e.EnvioId == envioId);
        if (envio != null)
        {
            return envio;
        }

        return null;
    }

    public async Task<ResultadoClasificacionDto> ClasificarBultosAsync(
        Guid envioId,
        ClasificarBultosRequestDto solicitud,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var url = $"/api/deposito/envios/{envioId}/clasificacion";
        HttpResponseMessage respuesta;

        try
        {
            respuesta = await _clienteHttp.PostAsJsonAsync(url, solicitud, OpcionesJson, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de comunicación al clasificar bultos: {ex.Message}");
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            try
            {
                using var doc = JsonDocument.Parse(detalleError);
                if (doc.RootElement.TryGetProperty("error", out var prop))
                {
                    detalleError = prop.GetString() ?? detalleError;
                }
            }
            catch
            {
                // Dejar detalleError como string original
            }

            throw new ApiExcepcion(respuesta.StatusCode, detalleError);
        }

        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoClasificacionDto>(OpcionesJson, cancelacion);
        if (resultado == null)
        {
            throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo interpretar el resultado de la clasificación.");
        }

        return resultado;
    }

    public async Task<IReadOnlyList<RutaPendienteRendicionDto>> ListarRutasPendientesRendicionAsync(CancellationToken cancelacion = default)
    {
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _clienteHttp.GetAsync("/api/deposito/hojas-de-ruta-pendientes-rendicion", cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de comunicación al consultar rutas pendientes de rendición: {ex.Message}");
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode}" : detalleError);
        }

        var lista = await respuesta.Content.ReadFromJsonAsync<List<RutaPendienteRendicionDto>>(OpcionesJson, cancelacion);
        return lista ?? (IReadOnlyList<RutaPendienteRendicionDto>)Array.Empty<RutaPendienteRendicionDto>();
    }

    public async Task<RutaParaRendicionDetalleDto?> ObtenerRutaParaRendicionAsync(Guid hojaDeRutaId, CancellationToken cancelacion = default)
    {
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _clienteHttp.GetAsync($"/api/deposito/hojas-de-ruta/{hojaDeRutaId}/rendicion", cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de comunicación al consultar detalle para rendición: {ex.Message}");
        }

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode}" : detalleError);
        }

        return await respuesta.Content.ReadFromJsonAsync<RutaParaRendicionDetalleDto>(OpcionesJson, cancelacion);
    }

    public async Task<ResultadoRendicionRutaDto> RendirHojaDeRutaAsync(
        Guid hojaDeRutaId,
        RendirHojaDeRutaRequestDto solicitud,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var url = $"/api/deposito/hojas-de-ruta/{hojaDeRutaId}/rendicion";
        HttpResponseMessage respuesta;

        try
        {
            respuesta = await _clienteHttp.PostAsJsonAsync(url, solicitud, OpcionesJson, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de comunicación al registrar la rendición: {ex.Message}");
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            try
            {
                using var doc = JsonDocument.Parse(detalleError);
                if (doc.RootElement.TryGetProperty("error", out var prop))
                {
                    detalleError = prop.GetString() ?? detalleError;
                }
            }
            catch
            {
                // Dejar detalleError como string original
            }

            throw new ApiExcepcion(respuesta.StatusCode, detalleError);
        }

        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoRendicionRutaDto>(OpcionesJson, cancelacion);
        if (resultado == null)
        {
            throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo interpretar el resultado de la rendición.");
        }

        return resultado;
    }

    private record DetalleEnvioRespuestaApi(
        Guid EnvioId,
        string CodigoSeguimiento,
        int EstadoActual,
        string ModalidadServicio,
        string NombreDestinatario,
        string DireccionDestino,
        DateTime FechaAlta,
        List<BultoDetalleApi>? Bultos);

    private record BultoDetalleApi(Guid BultoId, string Identificacion, decimal PesoKg, string EstadoBulto);
}
