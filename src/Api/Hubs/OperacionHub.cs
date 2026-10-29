using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace UltimaMilla.Api.Hubs;

/// <summary>
/// DTO con los datos de posición transmitidos desde la app móvil.
/// </summary>
public record TransmisionUbicacionRequest(
    Guid HojaDeRutaId,
    Guid RepartidorId,
    double Latitud,
    double Longitud,
    DateTime TimestampUtc
);

/// <summary>
/// Hub de SignalR para la supervisión y streaming de telemetría de operaciones en tiempo real (Hito 4 / RNF 6.10).
/// Permite recibir coordenadas GPS periódicas desde UltimaMilla.Mobile y retransmitirlas
/// al Tablero en Vivo del Backoffice de los despachadores.
/// </summary>
[Authorize]
public class OperacionHub : Hub
{
    private readonly ILogger<OperacionHub> _logger;

    public OperacionHub(ILogger<OperacionHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Método invocado por la app móvil para reportar la posición GPS periódica.
    /// </summary>
    public async Task TransmitirUbicacion(TransmisionUbicacionRequest payload)
    {
        _logger.LogInformation(
            "SignalR Hub: Coordenadas recibidas de Repartidor {RepartidorId} (Ruta {RutaId}): Lat={Lat:F5}, Lng={Lng:F5}",
            payload.RepartidorId, payload.HojaDeRutaId, payload.Latitud, payload.Longitud);

        // Retransmitir al canal del Backoffice para actualización en vivo del mapa
        await Clients.Others.SendAsync("PosicionRepartidorActualizada", payload);
    }

    /// <summary>
    /// Método con parámetros escalares para clientes que transmitan campos primitivos directos.
    /// </summary>
    public async Task TransmitirCoordenadas(Guid hojaDeRutaId, Guid repartidorId, double latitud, double longitud)
    {
        var request = new TransmisionUbicacionRequest(
            hojaDeRutaId,
            repartidorId,
            latitud,
            longitud,
            DateTime.UtcNow);

        await TransmitirUbicacion(request);
    }

    /// <summary>
    /// Permite a los despachadores del Backoffice suscribirse a los eventos de su operador específico.
    /// </summary>
    public async Task SuscribirAOperador(string operadorId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"operador-{operadorId}");
        _logger.LogInformation("SignalR Hub: Conexión {ConnectionId} unida al grupo operador {OperadorId}", Context.ConnectionId, operadorId);
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR Hub: Cliente conectado. ConnectionId: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(exception, "SignalR Hub: Cliente desconectado. ConnectionId: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
