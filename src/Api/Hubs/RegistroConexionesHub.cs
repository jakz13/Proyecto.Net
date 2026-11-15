using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Api.Hubs;

/// <summary>
/// Registro en memoria de conexiones SignalR activas por usuario y <c>sid</c>,
/// junto con la validación compartida de sesión única activa contra caché distribuida.
/// </summary>
public static class RegistroConexionesHub
{
    public const string HubOperacion = "operacion";
    public const string HubTablero = "tablero";

    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, (Guid UsuarioId, string? Sid)>> ConexionesPorHub =
        new(StringComparer.Ordinal);

    public static void Registrar(string hubNombre, string connectionId, Guid usuarioId, string? sid)
    {
        if (string.IsNullOrWhiteSpace(connectionId) || usuarioId == Guid.Empty)
            return;

        var mapa = ConexionesPorHub.GetOrAdd(hubNombre, _ => new ConcurrentDictionary<string, (Guid, string?)>(StringComparer.Ordinal));
        mapa[connectionId] = (usuarioId, sid);
    }

    public static void Remover(string hubNombre, string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            return;

        if (ConexionesPorHub.TryGetValue(hubNombre, out var mapa))
        {
            mapa.TryRemove(connectionId, out _);
        }
    }

    public static IReadOnlyList<string> ObtenerConexionesDesplazadas(
        string hubNombre,
        Guid usuarioId,
        string nuevoSessionId)
    {
        if (usuarioId == Guid.Empty
            || !ConexionesPorHub.TryGetValue(hubNombre, out var mapa))
        {
            return Array.Empty<string>();
        }

        var resultado = new List<string>();
        foreach (var kvp in mapa)
        {
            if (kvp.Value.UsuarioId == usuarioId
                && !string.Equals(kvp.Value.Sid, nuevoSessionId, StringComparison.Ordinal))
            {
                resultado.Add(kvp.Key);
            }
        }

        return resultado;
    }

    public static (Guid UsuarioId, string? Sid) ExtraerIdentidadYSid(ClaimsPrincipal? principal)
    {
        var rawUserId = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? principal?.FindFirstValue("sub");

        var usuarioId = Guid.TryParse(rawUserId, out var parsed) ? parsed : Guid.Empty;

        // Sesión única activa solo para repartidores: para el resto no hay identidad que registrar ni validar
        if (!PoliticaSesionUnica.AplicaA(principal))
            return (Guid.Empty, null);

        var sid = principal?.FindFirstValue("sid")
               ?? principal?.FindFirstValue("jti");

        return (usuarioId, sid);
    }

    /// <summary>
    /// Valida el claim <c>sid</c> contra Redis y registra el <c>sid</c> si aún no existe en caché.
    /// Devuelve <c>false</c> cuando el usuario posee otra sesión vigente distinta en Redis.
    /// </summary>
    public static async Task<bool> ValidarYRegistrarSesionAsync(
        IDistributedCacheService? cache,
        Guid usuarioId,
        string? sidToken,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        if (cache is null || usuarioId == Guid.Empty || string.IsNullOrWhiteSpace(sidToken))
            return true;

        try
        {
            var clave = ClavesCache.SesionActivaUsuario(usuarioId);
            var sesionVigente = await cache.GetAsync<string>(clave, cancellationToken);

            if (string.IsNullOrWhiteSpace(sesionVigente))
            {
                await cache.SetAsync(
                    clave,
                    sidToken,
                    ClavesCache.ExpiracionSesionActiva,
                    cancellationToken);
                return true;
            }

            return string.Equals(sesionVigente, sidToken, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Degradación controlada al validar sesión única en Hub para usuario {UsuarioId}.", usuarioId);
            return true;
        }
    }
}

