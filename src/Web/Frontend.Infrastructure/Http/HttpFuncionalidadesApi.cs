using System.Net.Http.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Funcionalidades;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>Cliente HTTP de las banderas de funcionalidad por operador.</summary>
public class HttpFuncionalidadesApi : IFuncionalidadesApi
{
    private readonly HttpClient _http;

    public HttpFuncionalidadesApi(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<OperadorFuncionalidadesDto>> ListarPorOperadorAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _http.GetAsync("api/plataforma/operadores", cancelacion);
        if (!respuesta.IsSuccessStatusCode)
            throw new ApiExcepcion(respuesta.StatusCode, $"No se pudieron consultar las funcionalidades ({(int)respuesta.StatusCode}).");

        var lista = await respuesta.Content.ReadFromJsonAsync<List<OperadorFuncionalidadesDto>>(cancellationToken: cancelacion);
        return lista ?? new List<OperadorFuncionalidadesDto>();
    }

    public async Task<IReadOnlyDictionary<string, bool>> ObtenerDelOperadorAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _http.GetAsync("api/funcionalidades", cancelacion);
        if (!respuesta.IsSuccessStatusCode)
            throw new ApiExcepcion(respuesta.StatusCode, $"No se pudieron consultar las funcionalidades ({(int)respuesta.StatusCode}).");

        return await respuesta.Content.ReadFromJsonAsync<Dictionary<string, bool>>(cancellationToken: cancelacion) ?? new Dictionary<string, bool>();
    }

    public async Task<Guid> CrearOperadorAsync(CrearOperadorDto dto, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.PostAsJsonAsync("api/plataforma/operadores", new
        {
            razonSocial = dto.RazonSocial,
            nombreComercial = dto.NombreComercial,
            administrador = new { nombre = dto.AdministradorNombre, email = dto.AdministradorEmail, password = dto.AdministradorPassword }
        }, cancelacion);

        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            string? mensaje = null;
            try { mensaje = System.Text.Json.JsonDocument.Parse(cuerpo).RootElement.GetProperty("error").GetString(); } catch { /* cuerpo sin forma conocida */ }
            throw new ApiExcepcion(respuesta.StatusCode, mensaje ?? $"No se pudo crear la empresa ({(int)respuesta.StatusCode}).");
        }

        return System.Text.Json.JsonDocument.Parse(cuerpo).RootElement.GetProperty("operadorId").GetGuid();
    }

    public async Task CambiarAsync(Guid operadorId, string clave, bool habilitada, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.PutAsJsonAsync($"api/plataforma/operadores/{operadorId}/funcionalidades/{Uri.EscapeDataString(clave)}", new { habilitada }, cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, $"No se pudo cambiar la funcionalidad: {detalle}");
        }
    }
}
