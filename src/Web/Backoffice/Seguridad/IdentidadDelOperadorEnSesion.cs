using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos;

namespace UltimaMilla.Backoffice.Seguridad;

/// <summary>
/// Marca (nombre, logo y colores) del operador de la sesión actual. Se pide a la API con el token del usuario, así que cada operador ve
/// la suya. Se guarda brevemente y conserva la última identidad conocida como respaldo ante desconexiones momentáneas.
/// </summary>
public sealed class IdentidadDelOperadorEnSesion
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(5);
    private static readonly ConcurrentDictionary<string, IdentidadVisualDto> UltimaIdentidadConocida = new(StringComparer.OrdinalIgnoreCase);

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

    public void InvalidarCache(string? operadorId = null)
    {
        var operador = operadorId ?? _contexto.HttpContext?.User.FindFirst("operador_id")?.Value ?? "sin-operador";
        _cache.Remove(operador);
        _cache.Remove("sin-operador");
        _cache.Remove("OP-01");
        _cache.Remove("OP-02");
    }

    public void ActualizarEnCache(IdentidadVisualDto nuevaIdentidad)
    {
        var operador = _contexto.HttpContext?.User.FindFirst("operador_id")?.Value ?? nuevaIdentidad.OperadorId;
        _cache.Set(operador, nuevaIdentidad, Vigencia);
        _cache.Set("sin-operador", nuevaIdentidad, Vigencia);
        UltimaIdentidadConocida[operador] = nuevaIdentidad;
        UltimaIdentidadConocida["sin-operador"] = nuevaIdentidad;
    }

    public async Task<IdentidadVisualDto> ObtenerAsync()
    {
        var operador = _contexto.HttpContext?.User.FindFirst("operador_id")?.Value ?? "sin-operador";

        if (_cache.TryGetValue(operador, out IdentidadVisualDto? existente) && existente is not null)
            return existente;

        try
        {
            var identidad = await _api.ObtenerIdentidadActualAsync();
            // Si la respuesta trajo logo vacío pero teníamos uno en memoria para el mismo operador, conservarlo si fue un fallo de contexto anónimo
            if (string.IsNullOrEmpty(identidad.LogoUrl)
                && string.IsNullOrEmpty(identidad.OperadorId)
                && UltimaIdentidadConocida.TryGetValue(operador, out var previa)
                && !string.IsNullOrEmpty(previa.LogoUrl))
            {
                identidad = previa;
            }
            else
            {
                UltimaIdentidadConocida[operador] = identidad;
            }

            _cache.Set(operador, identidad, Vigencia);
            return identidad;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo obtener la identidad visual del operador {Operador}.", operador);
            if (UltimaIdentidadConocida.TryGetValue(operador, out var respaldo)
                || UltimaIdentidadConocida.TryGetValue("sin-operador", out respaldo))
            {
                return respaldo;
            }

            return new IdentidadVisualDto();
        }
    }
}
