using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Hubs;

/// <summary>
/// Hub de SignalR para la difusión en tiempo real de hitos operativos del tablero.
/// Aísla las conexiones por operador logístico a partir de los claims verificados del token, nunca de parámetros enviados por el cliente.
/// </summary>
[Authorize]
public class TableroHub : Hub
{
    private readonly AppDbContext _db;

    public TableroHub(AppDbContext db)
    {
        _db = db;
    }

    private static readonly string[] ClaimsOperador =
    {
        "operador_id",
        "tenant_id",
        "OperadorId",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/tenantid"
    };

    private const string ClaimRelacionComercio = "comercio_operador_id";
    private const string ClaimComercio = "comercio_id";

    public static string GrupoOperador(Guid operadorId) => $"operador_{operadorId}";

    /// <summary>Grupo de un comercio dentro de su operador: recibe solo los eventos de sus propios envíos.</summary>
    public static string GrupoComercio(Guid comercioOperadorId) => $"comercio_{comercioOperadorId}";

    /// <summary>Grupo común a todas las sesiones de un comercio, sea cual sea el operador con el que operen (avisos sobre su lista de operadores).</summary>
    public static string GrupoCuentaComercio(Guid comercioId) => $"cuenta_comercio_{comercioId}";

    /// <summary>Grupo de marca del operador: recibe actualizaciones de identidad visual (nombre, colores, logo) tanto en Portal Comercio como en Backoffice.</summary>
    public static string GrupoIdentidadOperador(Guid operadorId) => $"identidad_operador_{operadorId}";

    public override async Task OnConnectedAsync()
    {
        if (!IntentarExtraerOperadorId(Context.User, out var operadorId))
        {
            Context.Abort();
            return;
        }

        // Un usuario de comercio solo ve lo suyo: se une a su grupo y NO al del operador (que incluye a los demás comercios)
        string grupo;
        if (EsUsuarioDeComercio(Context.User))
        {
            var relacion = await ResolverRelacionComercioAsync(Context.User, operadorId);
            if (relacion is null)
            {
                // Sesión de comercio sin relación vigente con el operador del token: no se le entrega nada
                Context.Abort();
                return;
            }

            grupo = GrupoComercio(relacion.Value.Relacion);
            await Groups.AddToGroupAsync(Context.ConnectionId, GrupoCuentaComercio(relacion.Value.Comercio));
        }
        else
        {
            grupo = GrupoOperador(operadorId);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, grupo);
        await Groups.AddToGroupAsync(Context.ConnectionId, GrupoIdentidadOperador(operadorId));
        await Clients.Caller.SendAsync("ConectadoTablero", new { OperadorId = operadorId, Grupo = grupo });
        await base.OnConnectedAsync();
    }

    internal static bool EsUsuarioDeComercio(ClaimsPrincipal? usuario) =>
        usuario?.Identity?.IsAuthenticated == true
        && (usuario.FindFirst(ClaimRelacionComercio) is not null || usuario.FindFirst(ClaimComercio) is not null);

    /// <summary>
    /// El grupo de un comercio se identifica por su relación con el operador (la que lleva cada envío). Según cómo se haya emitido el token,
    /// los claims de comercio traen esa relación o el id del comercio; en ambos casos se resuelve en la base acotada al operador del token,
    /// nunca a partir de lo que envíe el navegador.
    /// </summary>
    private async Task<(Guid Relacion, Guid Comercio)?> ResolverRelacionComercioAsync(ClaimsPrincipal? usuario, Guid operadorId)
    {
        var candidatos = new List<Guid>();
        foreach (var tipo in new[] { ClaimRelacionComercio, ClaimComercio })
        {
            if (Guid.TryParse(usuario?.FindFirst(tipo)?.Value, out var id) && id != Guid.Empty && !candidatos.Contains(id))
                candidatos.Add(id);
        }

        if (candidatos.Count == 0)
            return null;

        var vinculo = await _db.ComerciosOperadores.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.OperadorId == operadorId && (candidatos.Contains(c.Id) || candidatos.Contains(c.ComercioId)), Context.ConnectionAborted);
        return vinculo is null ? null : (vinculo.Id, vinculo.ComercioId);
    }

    internal static bool IntentarExtraerOperadorId(ClaimsPrincipal? usuario, out Guid operadorId)
    {
        operadorId = Guid.Empty;
        if (usuario?.Identity?.IsAuthenticated != true)
            return false;

        foreach (var tipoClaim in ClaimsOperador)
        {
            var valor = usuario.FindFirst(tipoClaim)?.Value;
            if (!string.IsNullOrWhiteSpace(valor) && Guid.TryParse(valor, out var parseado) && parseado != Guid.Empty)
            {
                operadorId = parseado;
                return true;
            }
        }

        return false;
    }
}

