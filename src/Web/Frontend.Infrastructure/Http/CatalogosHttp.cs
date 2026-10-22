using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Usuarios;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Consultas auxiliares para completar nombres (repartidores, zonas, usuarios) en las respuestas de la API operativa,
/// que identifica a esas entidades solo por su identificador. Son de mejor esfuerzo: si fallan, la pantalla muestra los datos sin el nombre.
/// </summary>
internal static class CatalogosHttp
{
    public static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    public static async Task<Dictionary<Guid, RepartidorDto>> RepartidoresAsync(HttpClient http, CancellationToken ct)
    {
        var lista = await LeerAsync<List<RepartidorDto>>(http, "/api/v1/flota/repartidores", ct);
        return lista?.ToDictionary(r => r.Id) ?? new Dictionary<Guid, RepartidorDto>();
    }

    public static async Task<Dictionary<Guid, VehiculoDto>> VehiculosAsync(HttpClient http, CancellationToken ct)
    {
        var lista = await LeerAsync<List<VehiculoDto>>(http, "/api/v1/flota/vehiculos", ct);
        return lista?.ToDictionary(v => v.Id) ?? new Dictionary<Guid, VehiculoDto>();
    }

    public static async Task<Dictionary<Guid, ZonaCoberturaDto>> ZonasAsync(HttpClient http, CancellationToken ct)
    {
        var lista = await LeerAsync<List<ZonaCoberturaDto>>(http, "/api/zonas-cobertura", ct);
        return lista?.ToDictionary(z => z.Id) ?? new Dictionary<Guid, ZonaCoberturaDto>();
    }

    public static async Task<Dictionary<Guid, string>> NombresDeUsuariosAsync(HttpClient http, CancellationToken ct)
    {
        var pagina = await LeerAsync<ResultadoPaginadoDto<UsuarioDto>>(http, "/api/usuarios?pagina=1&tamanoPagina=100", ct);
        return pagina?.Elementos.ToDictionary(u => u.Id, u => u.Nombre) ?? new Dictionary<Guid, string>();
    }

    private static async Task<T?> LeerAsync<T>(HttpClient http, string url, CancellationToken ct) where T : class
    {
        try
        {
            var respuesta = await http.GetAsync(url, ct);
            return respuesta.IsSuccessStatusCode ? await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson, ct) : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
