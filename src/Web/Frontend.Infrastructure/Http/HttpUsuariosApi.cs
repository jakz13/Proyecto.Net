using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Usuarios;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Implementación basada en HTTP/REST de IUsuariosApi.
/// Conecta el Backoffice con los endpoints de gestión de usuarios y roles del backend (UC-01, RF-31).
/// </summary>
public class HttpUsuariosApi : IUsuariosApi
{
    private readonly HttpClient _clienteHttp;
    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    public HttpUsuariosApi(HttpClient clienteHttp)
    {
        _clienteHttp = clienteHttp;
    }

    public async Task<ResultadoPaginadoDto<UsuarioDto>> ListarUsuariosAsync(FiltroUsuariosDto? filtro = null, CancellationToken cancelacion = default)
    {
        filtro ??= new FiltroUsuariosDto();
        var url = $"/api/usuarios?pagina={filtro.Pagina}&tamanoPagina={filtro.TamanoPagina}";
        if (!string.IsNullOrWhiteSpace(filtro.TextoBusqueda))
        {
            url += $"&q={Uri.EscapeDataString(filtro.TextoBusqueda)}";
        }
        if (filtro.PerfilId.HasValue && filtro.PerfilId.Value != Guid.Empty)
        {
            url += $"&perfilId={filtro.PerfilId.Value}";
        }
        if (filtro.SoloActivos.HasValue)
        {
            url += $"&soloActivos={filtro.SoloActivos.Value}";
        }

        var respuesta = await _clienteHttp.GetAsync(url, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoPaginadoDto<UsuarioDto>>(OpcionesJson, cancelacion);
        return resultado ?? new ResultadoPaginadoDto<UsuarioDto>();
    }

    public async Task<UsuarioDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.GetAsync($"/api/usuarios/{id}", cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<UsuarioDto>(OpcionesJson, cancelacion);
    }

    public async Task<UsuarioDto> CrearUsuarioAsync(CrearUsuarioDto nuevoUsuario, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PostAsJsonAsync("/api/usuarios", nuevoUsuario, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var creado = await respuesta.Content.ReadFromJsonAsync<UsuarioDto>(OpcionesJson, cancelacion);
        return creado ?? throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo deserializar el usuario creado.");
    }

    public async Task<UsuarioDto> ActualizarUsuarioAsync(Guid id, ActualizarUsuarioDto usuarioActualizado, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PutAsJsonAsync($"/api/usuarios/{id}", usuarioActualizado, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var actualizado = await respuesta.Content.ReadFromJsonAsync<UsuarioDto>(OpcionesJson, cancelacion);
        return actualizado ?? throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo deserializar el usuario actualizado.");
    }

    public async Task CambiarEstadoUsuarioAsync(Guid id, bool activo, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PatchAsJsonAsync($"/api/usuarios/{id}/estado", new { Activo = activo }, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);
    }

    public async Task AsignarPerfilAsync(Guid id, Guid perfilId, CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.PutAsJsonAsync($"/api/usuarios/{id}/perfil", new AsignarPerfilDto { PerfilId = perfilId }, cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);
    }

    public async Task<IReadOnlyList<PerfilDto>> ListarPerfilesAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _clienteHttp.GetAsync("/api/perfiles", cancelacion);
        await AsegurarRespuestaExitosaAsync(respuesta, cancelacion);

        var perfiles = await respuesta.Content.ReadFromJsonAsync<List<PerfilDto>>(OpcionesJson, cancelacion);
        return perfiles ?? (IReadOnlyList<PerfilDto>)Array.Empty<PerfilDto>();
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
