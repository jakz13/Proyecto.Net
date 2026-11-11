using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Ports;

/// <summary>
/// Banderas de funcionalidad por operador. La lectura usa el caché compartido con expiración corta: un cambio llega a todas las instancias
/// de la API y al worker de inmediato (el cambio invalida la entrada compartida) y, como máximo, en <see cref="TiempoMaximoDePropagacion"/>
/// aunque la invalidación fallara, sin reiniciar nada.
/// </summary>
public interface IFuncionalidadesService
{
    /// <summary>Cota superior, conocida, de la propagación de un cambio de bandera.</summary>
    static readonly TimeSpan TiempoMaximoDePropagacion = TimeSpan.FromSeconds(30);

    Task<bool> EstaHabilitadaAsync(Guid operadorId, string clave, CancellationToken cancellationToken = default);

    /// <summary>Todas las funcionalidades del catálogo con su valor para el operador (las que no tienen registro están habilitadas).</summary>
    Task<IReadOnlyDictionary<string, bool>> ObtenerDelOperadorAsync(Guid operadorId, CancellationToken cancellationToken = default);

    /// <summary>Cambia una bandera, registra quién y cuándo, e invalida la entrada compartida. Devuelve false si no hubo cambio.</summary>
    Task<bool> EstablecerAsync(Guid operadorId, string clave, bool habilitada, Guid? usuarioId, string? usuarioNombre, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistorialBandera>> HistorialAsync(Guid operadorId, CancellationToken cancellationToken = default);
}

/// <summary>Se lanza cuando se usa una funcionalidad que el operador no tiene habilitada.</summary>
public static class FuncionalidadesExtensiones
{
    public static async Task ExigirAsync(this IFuncionalidadesService? servicio, Guid operadorId, string clave, CancellationToken cancellationToken = default)
    {
        if (servicio is null)
            return;

        if (!await servicio.EstaHabilitadaAsync(operadorId, clave, cancellationToken))
        {
            var nombre = Funcionalidad.Catalogo.TryGetValue(clave, out var n) ? n : clave;
            throw new DomainValidationException($"La funcionalidad '{nombre}' no está habilitada para su operador.");
        }
    }
}
