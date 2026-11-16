using System.Security.Claims;

namespace UltimaMilla.Application.Common;

/// <summary>
/// Sesión única activa por cuenta: un nuevo inicio de sesión deja sin efecto los tokens anteriores de esa cuenta, para TODOS los perfiles
/// (administrador, despachador, operario, repartidor y comercio). Dos personas no pueden trabajar a la vez con la misma cuenta;
/// la sesión desplazada recibe 401 con código SESION_DESPLAZADA y el Backoffice, el Portal y la app móvil la llevan al inicio de sesión.
/// Es el único criterio: el inicio de sesión, el middleware y los hubs de SignalR lo consultan.
/// </summary>
public static class PoliticaSesionUnica
{
    public const string PerfilRepartidor = "Repartidor";

    /// <summary>Todo perfil con nombre queda sujeto a la sesión única.</summary>
    public static bool AplicaAlPerfil(string? nombrePerfilORol)
        => !string.IsNullOrWhiteSpace(nombrePerfilORol);

    /// <summary>Para una identidad ya autenticada (claims del token): aplica si tiene algún rol.</summary>
    public static bool AplicaA(ClaimsPrincipal? usuario)
        => usuario?.Identity?.IsAuthenticated == true
           && usuario.Claims.Any(c => (c.Type == ClaimTypes.Role || c.Type == "role") && AplicaAlPerfil(c.Value));
}
