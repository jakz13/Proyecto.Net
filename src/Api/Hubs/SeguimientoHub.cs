using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Hubs;

/// <summary>
/// Tiempo real del seguimiento público: el destinatario, sin cuenta, se suscribe al código de SU envío y recibe
/// una señal cuando cambia de estado. La señal no lleva datos del envío: la página vuelve a consultar el endpoint público, que ya
/// aplica todas las reglas de privacidad.
///
/// Protección frente a la enumeración de códigos:
///  - La suscripción nunca informa si el código existe: responde igual para códigos válidos e inexistentes.
///  - Cada conexión puede seguir pocos códigos.
///  - Las conexiones al hub comparten el límite de tasa por IP de la consulta pública.
/// </summary>
[AllowAnonymous]
public class SeguimientoHub : Hub
{
    public const int MaximoCodigosPorConexion = 3;
    private const string ClaveSuscripciones = "codigos";

    // Misma forma que los códigos públicos (por ejemplo ENV-2026-00108 o TRK-A6EC310FD5)
    private static readonly Regex FormatoCodigo = new("^[A-Z0-9][A-Z0-9-]{4,39}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Grupo(string codigoNormalizado) => $"seguimiento_{codigoNormalizado}";

    /// <summary>Quienes siguen un envío del operador: reciben la señal de que una funcionalidad del operador cambió (por ejemplo, el botón de reprogramar).</summary>
    public static string GrupoOperador(Guid operadorId) => $"seguimiento_operador_{operadorId:N}";

    private readonly AppDbContext _db;

    public SeguimientoHub(AppDbContext db) => _db = db;

    public static string? Normalizar(string? codigo)
    {
        var limpio = codigo?.Trim().ToUpperInvariant();
        return !string.IsNullOrEmpty(limpio) && FormatoCodigo.IsMatch(limpio) ? limpio : null;
    }

    /// <summary>Suscribe la conexión al código indicado. Devuelve siempre <c>true</c> si la petición es válida, exista o no el envío.</summary>
    public async Task<bool> Seguir(string codigo)
    {
        var normalizado = Normalizar(codigo);
        if (normalizado is null)
            return false;

        var suscritos = Context.Items.TryGetValue(ClaveSuscripciones, out var existentes) && existentes is HashSet<string> conjunto
            ? conjunto
            : new HashSet<string>();
        Context.Items[ClaveSuscripciones] = suscritos;

        if (!suscritos.Contains(normalizado))
        {
            if (suscritos.Count >= MaximoCodigosPorConexion)
                return false;

            suscritos.Add(normalizado);
            await Groups.AddToGroupAsync(Context.ConnectionId, Grupo(normalizado));

            // Además del envío, se sigue a su operador para enterarse al instante de que una funcionalidad se habilitó o deshabilitó.
            // Si el código no existe no pasa nada y la respuesta es la misma (no se revela si el envío existe).
            var operadorId = await _db.Envios.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.CodigoSeguimientoPublico == normalizado)
                .Select(e => (Guid?)e.OperadorId)
                .FirstOrDefaultAsync();
            if (operadorId is { } operador && operador != Guid.Empty)
                await Groups.AddToGroupAsync(Context.ConnectionId, GrupoOperador(operador));
        }

        return true;
    }

    /// <summary>Deja de seguir un código (por ejemplo, cuando el usuario busca otro envío).</summary>
    public async Task Dejar(string codigo)
    {
        var normalizado = Normalizar(codigo);
        if (normalizado is null)
            return;

        if (Context.Items.TryGetValue(ClaveSuscripciones, out var existentes) && existentes is HashSet<string> conjunto)
            conjunto.Remove(normalizado);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupo(normalizado));
    }
}
