using System.Security.Claims;

namespace UltimaMilla.Application.Common;

/// <summary>
/// La sesión única activa (un nuevo inicio de sesión deja sin efecto los tokens anteriores de la cuenta) se aplica SOLO a los repartidores, que usan la app móvil
/// en un único dispositivo. El resto de los perfiles (administrador, despachador, operario, comercio...) puede tener varias sesiones simultáneas con la misma cuenta,
/// por ejemplo en el Backoffice desde dos navegadores o en una demostración con cuentas compartidas.
/// Es el único criterio: el inicio de sesión, el middleware y los hubs de SignalR lo consultan.
/// </summary>
public static class PoliticaSesionUnica
{
    public const string PerfilRepartidor = "Repartidor";

    public static bool AplicaAlPerfil(string? nombrePerfilORol)
        => string.Equals(nombrePerfilORol?.Trim(), PerfilRepartidor, StringComparison.OrdinalIgnoreCase);

    /// <summary>Para una identidad ya autenticada (claims del token): aplica si alguno de sus roles es Repartidor.</summary>
    public static bool AplicaA(ClaimsPrincipal? usuario)
        => usuario?.Identity?.IsAuthenticated == true
           && usuario.Claims.Any(c => (c.Type == ClaimTypes.Role || c.Type == "role") && AplicaAlPerfil(c.Value));
}
