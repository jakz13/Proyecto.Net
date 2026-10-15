using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.MotivosNoEntrega;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Implementación basada en cliente HTTP REST de IMotivosNoEntregaApi (UC-05, RF-04, RF-21).
/// Facilita la conexión directa con los endpoints de la API backend cuando esté disponible.
/// </summary>
public class HttpMotivosNoEntregaApi : IMotivosNoEntregaApi
{
    private readonly HttpClient _clienteHttp;
    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    public HttpMotivosNoEntregaApi(HttpClient clienteHttp)
    {
        _clienteHttp = clienteHttp;
    }

    public async Task<IReadOnlyList<MotivoNoEntregaDto>> ListarMotivosAsync(FiltroMotivosNoEntregaDto? filtro = null, CancellationToken cancellationToken = default)
    {
        var url = "/api/v1/motivos-no-entrega";
        var parametros = new List<string>();

        if (filtro != null)
        {
            if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
            {
                parametros.Add($"busqueda={Uri.EscapeDataString(filtro.Busqueda)}");
            }

            if (filtro.SoloActivos.HasValue)
            {
                parametros.Add($"soloActivos={filtro.SoloActivos.Value}");
            }
        }

        if (parametros.Count > 0)
        {
            url += "?" + string.Join("&", parametros);
        }

        var respuesta = await _clienteHttp.GetAsync(url, cancellationToken);
        await AsegurarRespuestaExitosaAsync(respuesta, cancellationToken);

        var lista = await respuesta.Content.ReadFromJsonAsync<List<MotivoNoEntregaDto>>(OpcionesJson, cancellationToken);
        return lista ?? (IReadOnlyList<MotivoNoEntregaDto>)Array.Empty<MotivoNoEntregaDto>();
    }

    public async Task<MotivoNoEntregaDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var respuesta = await _clienteHttp.GetAsync($"/api/v1/motivos-no-entrega/{id}", cancellationToken);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await AsegurarRespuestaExitosaAsync(respuesta, cancellationToken);
        return await respuesta.Content.ReadFromJsonAsync<MotivoNoEntregaDto>(OpcionesJson, cancellationToken);
    }

    public async Task<MotivoNoEntregaDto> CrearMotivoAsync(CrearMotivoNoEntregaDto dto, CancellationToken cancellationToken = default)
    {
        var respuesta = await _clienteHttp.PostAsJsonAsync("/api/v1/motivos-no-entrega", dto, cancellationToken);
        await AsegurarRespuestaExitosaAsync(respuesta, cancellationToken);

        var creado = await respuesta.Content.ReadFromJsonAsync<MotivoNoEntregaDto>(OpcionesJson, cancellationToken);
        return creado ?? throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo deserializar el motivo creado.");
    }

    public async Task<MotivoNoEntregaDto> ActualizarMotivoAsync(ActualizarMotivoNoEntregaDto dto, CancellationToken cancellationToken = default)
    {
        var respuesta = await _clienteHttp.PutAsJsonAsync($"/api/v1/motivos-no-entrega/{dto.Id}", dto, cancellationToken);
        await AsegurarRespuestaExitosaAsync(respuesta, cancellationToken);

        var actualizado = await respuesta.Content.ReadFromJsonAsync<MotivoNoEntregaDto>(OpcionesJson, cancellationToken);
        return actualizado ?? throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo deserializar el motivo actualizado.");
    }

    public async Task CambiarEstadoAsync(Guid id, bool activo, CancellationToken cancellationToken = default)
    {
        var contenido = JsonContent.Create(new { activo });
        var respuesta = await _clienteHttp.PatchAsync($"/api/v1/motivos-no-entrega/{id}/estado", contenido, cancellationToken);
        await AsegurarRespuestaExitosaAsync(respuesta, cancellationToken);
    }

    public async Task<ResumenMotivosNoEntregaDto> ObtenerResumenAsync(CancellationToken cancellationToken = default)
    {
        var respuesta = await _clienteHttp.GetAsync("/api/v1/motivos-no-entrega/resumen", cancellationToken);
        await AsegurarRespuestaExitosaAsync(respuesta, cancellationToken);

        var resumen = await respuesta.Content.ReadFromJsonAsync<ResumenMotivosNoEntregaDto>(OpcionesJson, cancellationToken);
        return resumen ?? new ResumenMotivosNoEntregaDto();
    }

    public async Task<IReadOnlyList<MotivoNoEntregaDto>> ObtenerMotivosParaSincronizacionMovilAsync(CancellationToken cancellationToken = default)
    {
        var respuesta = await _clienteHttp.GetAsync("/api/v1/motivos-no-entrega/sincronizacion-movil", cancellationToken);
        await AsegurarRespuestaExitosaAsync(respuesta, cancellationToken);

        var lista = await respuesta.Content.ReadFromJsonAsync<List<MotivoNoEntregaDto>>(OpcionesJson, cancellationToken);
        return lista ?? (IReadOnlyList<MotivoNoEntregaDto>)Array.Empty<MotivoNoEntregaDto>();
    }

    private static async Task AsegurarRespuestaExitosaAsync(HttpResponseMessage respuesta, CancellationToken cancellationToken)
    {
        if (respuesta.IsSuccessStatusCode)
        {
            return;
        }

        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancellationToken);
        throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(cuerpo) ? $"Error HTTP {respuesta.StatusCode}" : cuerpo);
    }
}
