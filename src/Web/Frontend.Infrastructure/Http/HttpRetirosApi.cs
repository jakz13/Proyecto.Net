using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Retiros;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>Cliente HTTP del retiro en domicilio: portal del comercio y backoffice del operador.</summary>
public class HttpRetirosApi : IRetirosApi
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public HttpRetirosApi(HttpClient http)
    {
        _http = http;
    }

    // ----- Portal del comercio -----

    public async Task<ResultadoSolicitudRetiroDto> SolicitarAsync(SolicitarRetiroDto solicitud, CancellationToken cancelacion = default)
    {
        var cuerpo = new
        {
            nombreContacto = solicitud.NombreContacto,
            telefonoContacto = solicitud.TelefonoContacto,
            emailContacto = solicitud.EmailContacto,
            calle = solicitud.Calle,
            numero = solicitud.Numero,
            ciudad = solicitud.Ciudad,
            referencia = solicitud.Referencia,
            motivo = solicitud.Motivo,
            fechaSolicitada = solicitud.FechaSolicitada?.ToString("yyyy-MM-dd"),
            referenciaExterna = solicitud.ReferenciaExterna,
            bultos = solicitud.Bultos.Select(b => new { descripcion = b.Descripcion, peso = b.Peso, alto = b.Alto, ancho = b.Ancho, profundo = b.Profundo })
        };

        var respuesta = await _http.PostAsJsonAsync("/api/comercio/retiros", cuerpo, Opciones, cancelacion);
        await AsegurarAsync(respuesta, cancelacion);
        return (await respuesta.Content.ReadFromJsonAsync<ResultadoSolicitudRetiroDto>(Opciones, cancelacion))!;
    }

    public async Task<PaginaRetirosDto> ListarDelComercioAsync(string? estado = null, CancellationToken cancelacion = default)
    {
        var url = string.IsNullOrWhiteSpace(estado) ? "/api/comercio/retiros?tamano=100" : $"/api/comercio/retiros?tamano=100&estado={Uri.EscapeDataString(estado)}";
        var respuesta = await _http.GetAsync(url, cancelacion);
        await AsegurarAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<PaginaRetirosDto>(Opciones, cancelacion) ?? new PaginaRetirosDto();
    }

    public Task<RetiroDetalleDto?> ObtenerDelComercioAsync(Guid id, CancellationToken cancelacion = default) =>
        ObtenerAsync($"/api/comercio/retiros/{id}", cancelacion);

    public Task<RetiroDetalleDto> CancelarDelComercioAsync(Guid id, string motivo, CancellationToken cancelacion = default) =>
        EnviarAsync($"/api/comercio/retiros/{id}/cancelar", new { motivo }, cancelacion);

    // ----- Backoffice del operador -----

    public async Task<ResumenRetirosOperadorDto> ListarDelOperadorAsync(string? estado = null, CancellationToken cancelacion = default)
    {
        var url = string.IsNullOrWhiteSpace(estado) ? "/api/retiros" : $"/api/retiros?estado={Uri.EscapeDataString(estado)}";
        var respuesta = await _http.GetAsync(url, cancelacion);
        await AsegurarAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<ResumenRetirosOperadorDto>(Opciones, cancelacion) ?? new ResumenRetirosOperadorDto();
    }

    public Task<RetiroDetalleDto?> ObtenerDelOperadorAsync(Guid id, CancellationToken cancelacion = default) =>
        ObtenerAsync($"/api/retiros/{id}", cancelacion);

    public Task<RetiroDetalleDto> RecibirEnDepositoAsync(Guid id, CancellationToken cancelacion = default) =>
        EnviarAsync($"/api/retiros/{id}/recepcion-deposito", new { }, cancelacion);

    public Task<RetiroDetalleDto> EntregarAlComercioAsync(Guid id, string? receptor, CancellationToken cancelacion = default) =>
        EnviarAsync($"/api/retiros/{id}/entrega-comercio", new { receptor }, cancelacion);

    public Task<RetiroDetalleDto> ReprogramarAsync(Guid id, CancellationToken cancelacion = default) =>
        EnviarAsync($"/api/retiros/{id}/reprogramar", new { }, cancelacion);

    public Task<RetiroDetalleDto> SacarDeRutaAsync(Guid id, CancellationToken cancelacion = default) =>
        EnviarAsync($"/api/retiros/{id}/sacar-de-ruta", new { }, cancelacion);

    public Task<RetiroDetalleDto> CancelarDelOperadorAsync(Guid id, string motivo, CancellationToken cancelacion = default) =>
        EnviarAsync($"/api/retiros/{id}/cancelar", new { motivo }, cancelacion);

    // ----- Utilidades -----

    private async Task<RetiroDetalleDto?> ObtenerAsync(string url, CancellationToken cancelacion)
    {
        var respuesta = await _http.GetAsync(url, cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return null;

        await AsegurarAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<RetiroDetalleDto>(Opciones, cancelacion);
    }

    private async Task<RetiroDetalleDto> EnviarAsync(string url, object cuerpo, CancellationToken cancelacion)
    {
        var respuesta = await _http.PostAsJsonAsync(url, cuerpo, Opciones, cancelacion);
        await AsegurarAsync(respuesta, cancelacion);
        return (await respuesta.Content.ReadFromJsonAsync<RetiroDetalleDto>(Opciones, cancelacion))!;
    }

    /// <summary>Traduce el cuerpo { "error": "..." } de la API al mensaje que ve el usuario.</summary>
    private static async Task AsegurarAsync(HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        if (respuesta.IsSuccessStatusCode)
            return;

        var texto = await respuesta.Content.ReadAsStringAsync(cancelacion);
        var mensaje = texto;
        try
        {
            using var documento = JsonDocument.Parse(texto);
            if (documento.RootElement.ValueKind == JsonValueKind.Object && documento.RootElement.TryGetProperty("error", out var error))
                mensaje = error.GetString() ?? texto;
        }
        catch (JsonException)
        {
            // Cuerpo que no es JSON: se muestra tal cual
        }

        throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(mensaje) ? $"La operación falló ({(int)respuesta.StatusCode})." : mensaje);
    }
}
