using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace UltimaMilla.Api.Hubs;

/// <summary>
/// Hub de SignalR para la difusión en tiempo real de hitos operativos del tablero (UC-12, RF-29).
/// Aísla las conexiones por operador logístico a partir de los claims verificados del token, nunca de parámetros enviados por el cliente.
/// </summary>
[Authorize]
public class TableroHub : Hub
{
    private static readonly string[] ClaimsOperador =
    {
        "operador_id",
        "tenant_id",
        "OperadorId",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/tenantid"
    };

    public static string GrupoOperador(Guid operadorId) => $"operador_{operadorId}";

    public override async Task OnConnectedAsync()
    {
        if (!IntentarExtraerOperadorId(Context.User, out var operadorId))
        {
            Context.Abort();
            return;
        }

        var grupo = GrupoOperador(operadorId);
        await Groups.AddToGroupAsync(Context.ConnectionId, grupo);
        await Clients.Caller.SendAsync("ConectadoTablero", new { OperadorId = operadorId, Grupo = grupo });
        await base.OnConnectedAsync();
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

