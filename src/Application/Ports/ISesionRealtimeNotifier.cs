namespace UltimaMilla.Application.Ports;

/// <summary>
/// Puerto para notificar en tiempo real el desplazamiento de una sesión anterior
/// cuando el mismo usuario inicia una nueva sesión válida en otro dispositivo.
/// </summary>
public interface ISesionRealtimeNotifier
{
    /// <summary>
    /// Notifica a las conexiones activas previas del usuario que su sesión fue desplazada.
    /// </summary>
    Task NotificarSesionDesplazadaAsync(
        Guid usuarioId,
        string nuevoSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Cierra en el momento las sesiones abiertas de un usuario (suspendido o con otro perfil) con un mensaje que explica por qué.</summary>
    Task NotificarSesionRevocadaAsync(Guid usuarioId, string mensaje, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
