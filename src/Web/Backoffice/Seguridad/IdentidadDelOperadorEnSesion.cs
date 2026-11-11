using Microsoft.Extensions.Caching.Memory;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos;

namespace UltimaMilla.Backoffice.Seguridad;

/// <summary>
/// Marca (nombre, logo y colores) del operador de la sesión actual. Se pide a la API con el token del usuario, así que cada operador ve
/// la suya; antes el menú mostraba siempre la del operador de demostración. Se guarda un minuto por operador para no consultar en cada página.
/// </summary>
public sealed class IdentidadDelOperadorEnSesion
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(1);

    private readonly IIdentidadVisualApi _api;
    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor _contexto;
    private readonly ILogger<IdentidadDelOperadorEnSesion> _logger;

    public IdentidadDelOperadorEnSesion(
        IIdentidadVisualApi api,
        IMemoryCache cache,
        IHttpContextAccessor contexto,
        ILogger<IdentidadDelOperadorEnSesion> logger)
    {
        _api = api;
        _cache = cache;
        _contexto = contexto;
        _logger = logger;
    }

    public async Task<IdentidadVisualDto> ObtenerAsync()
    {
        var operador = _contexto.HttpContext?.User.FindFirst("operador_id")?.Value ?? "sin-operador";

        if (_cache.TryGetValue(operador, out IdentidadVisualDto? existente) && existente is not null)
            return existente;

        try
        {
            var identidad = await _api.ObtenerIdentidadActualAsync();
            _cache.Set(operador, identidad, Vigencia);
            return identidad;
        }
        catch (Exception ex)
        {
            // La marca es decorativa: si la API no responde, la página se muestra con la marca por defecto
            _logger.LogWarning(ex, "No se pudo obtener la identidad visual del operador {Operador}.", operador);
            return new IdentidadVisualDto();
        }
    }
}
