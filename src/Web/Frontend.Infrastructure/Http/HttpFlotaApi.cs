using System.Net.Http.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Cliente HTTP para consumir los endpoints de flota, vehículos y repartidores de la API backend (RF-06).
/// </summary>
public class HttpFlotaApi : IFlotaApi
{
    private readonly HttpClient _http;

    public HttpFlotaApi(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<VehiculoDto>> ListarVehiculosAsync(bool? soloActivos = null, CancellationToken cancelacion = default)
    {
        var ruta = soloActivos.HasValue ? $"api/v1/flota/vehiculos?soloActivos={soloActivos.Value}" : "api/v1/flota/vehiculos";
        var resultado = await _http.GetFromJsonAsync<List<VehiculoDto>>(ruta, cancelacion);
        return resultado ?? new List<VehiculoDto>();
    }

    public async Task<VehiculoDto?> ObtenerVehiculoPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        return await _http.GetFromJsonAsync<VehiculoDto>($"api/v1/flota/vehiculos/{id}", cancelacion);
    }

    public async Task<VehiculoDto> CrearVehiculoAsync(CrearVehiculoDto dto, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.PostAsJsonAsync("api/v1/flota/vehiculos", dto, cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion($"Error al registrar el vehículo: {detalle}");
        }

        var creado = await respuesta.Content.ReadFromJsonAsync<VehiculoDto>(cancellationToken: cancelacion);
        return creado ?? throw new ApiExcepcion("No se pudo deserializar el vehículo creado.");
    }

    public async Task<VehiculoDto> ActualizarVehiculoAsync(ActualizarVehiculoDto dto, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.PutAsJsonAsync($"api/v1/flota/vehiculos/{dto.Id}", dto, cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion($"Error al actualizar el vehículo: {detalle}");
        }

        var actualizado = await respuesta.Content.ReadFromJsonAsync<VehiculoDto>(cancellationToken: cancelacion);
        return actualizado ?? throw new ApiExcepcion("No se pudo deserializar el vehículo actualizado.");
    }

    public async Task CambiarEstadoVehiculoAsync(Guid id, bool activo, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.PatchAsJsonAsync($"api/v1/flota/vehiculos/{id}/estado", new { activo }, cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion($"Error al cambiar estado del vehículo: {detalle}");
        }
    }

    public async Task<IReadOnlyList<RepartidorDto>> ListarRepartidoresAsync(string? estado = null, CancellationToken cancelacion = default)
    {
        var ruta = !string.IsNullOrWhiteSpace(estado) ? $"api/v1/flota/repartidores?estado={Uri.EscapeDataString(estado)}" : "api/v1/flota/repartidores";
        var resultado = await _http.GetFromJsonAsync<List<RepartidorDto>>(ruta, cancelacion);
        return resultado ?? new List<RepartidorDto>();
    }

    public async Task<RepartidorDto?> ObtenerRepartidorPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        return await _http.GetFromJsonAsync<RepartidorDto>($"api/v1/flota/repartidores/{id}", cancelacion);
    }

    public async Task<RepartidorDto> CrearRepartidorAsync(CrearRepartidorDto dto, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.PostAsJsonAsync("api/v1/flota/repartidores", dto, cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion($"Error al registrar el repartidor: {detalle}");
        }

        var creado = await respuesta.Content.ReadFromJsonAsync<RepartidorDto>(cancellationToken: cancelacion);
        return creado ?? throw new ApiExcepcion("No se pudo deserializar el repartidor creado.");
    }

    public async Task<RepartidorDto> ActualizarRepartidorAsync(ActualizarRepartidorDto dto, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.PutAsJsonAsync($"api/v1/flota/repartidores/{dto.Id}", dto, cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion($"Error al actualizar el repartidor: {detalle}");
        }

        var actualizado = await respuesta.Content.ReadFromJsonAsync<RepartidorDto>(cancellationToken: cancelacion);
        return actualizado ?? throw new ApiExcepcion("No se pudo deserializar el repartidor actualizado.");
    }

    public async Task AsignarVehiculoAsync(Guid repartidorId, Guid? vehiculoId, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.PutAsJsonAsync($"api/v1/flota/repartidores/{repartidorId}/vehiculo", new { vehiculoId }, cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion($"Error al asociar vehículo al repartidor: {detalle}");
        }
    }

    public async Task CambiarEstadoRepartidorAsync(Guid repartidorId, string nuevoEstado, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.PatchAsJsonAsync($"api/v1/flota/repartidores/{repartidorId}/estado", new { nuevoEstado }, cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion($"Error al cambiar estado del repartidor: {detalle}");
        }
    }
}
