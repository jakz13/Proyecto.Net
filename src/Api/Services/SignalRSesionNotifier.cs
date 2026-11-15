using Microsoft.AspNetCore.SignalR;
using UltimaMilla.Api.Auth;
using UltimaMilla.Api.Hubs;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Api.Services;

/// <summary>
/// Adaptador SignalR para <see cref="ISesionRealtimeNotifier"/> que expulsa
/// en tiempo real las conexiones desplazadas en <see cref="OperacionHub"/> y <see cref="TableroHub"/>.
/// </summary>
public sealed class SignalRSesionNotifier : ISesionRealtimeNotifier
{
    public const string EventoSesionDesplazada = "SesionDesplazada";

    private readonly IHubContext<OperacionHub> _operacionHub;
    private readonly IHubContext<TableroHub> _tableroHub;
    private readonly ILogger<SignalRSesionNotifier> _logger;

    public SignalRSesionNotifier(
        IHubContext<OperacionHub> operacionHub,
        IHubContext<TableroHub> tableroHub,
        ILogger<SignalRSesionNotifier> logger)
    {
        _operacionHub = operacionHub ?? throw new ArgumentNullException(nameof(operacionHub));
        _tableroHub = tableroHub ?? throw new ArgumentNullException(nameof(tableroHub));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotificarSesionDesplazadaAsync(
        Guid usuarioId,
        string nuevoSessionId,
        CancellationToken cancellationToken = default)
    {
        if (usuarioId == Guid.Empty || string.IsNullOrWhiteSpace(nuevoSessionId))
            return;

        await NotificarHubAsync(
            _operacionHub,
            RegistroConexionesHub.HubOperacion,
            usuarioId,
            nuevoSessionId,
            cancellationToken);

        await NotificarHubAsync(
            _tableroHub,
            RegistroConexionesHub.HubTablero,
            usuarioId,
            nuevoSessionId,
            cancellationToken);
    }

    private async Task NotificarHubAsync<THub>(
        IHubContext<THub> hubContext,
        string hubNombre,
        Guid usuarioId,
        string nuevoSessionId,
        CancellationToken cancellationToken) where THub : Hub
    {
        var conexiones = RegistroConexionesHub.ObtenerConexionesDesplazadas(
            hubNombre,
            usuarioId,
            nuevoSessionId);

        if (conexiones.Count == 0)
            return;

        try
        {
            await hubContext.Clients.Clients(conexiones).SendAsync(
                EventoSesionDesplazada,
                SesionUnicaActivaMiddleware.MensajeSesionDesplazada,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Fallo no bloqueante al notificar desplazamiento de sesión en hub {HubNombre} para usuario {UsuarioId}.",
                hubNombre,
                usuarioId);
        }
    }
}

