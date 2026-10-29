using Microsoft.AspNetCore.SignalR.Client;

namespace UltimaMilla.Mobile.Services.Realtime;

/// <summary>
/// DTO con la telemetría geográfica del repartidor para streaming en tiempo real.
/// </summary>
public record UbicacionTransmisionDto(
    Guid HojaDeRutaId,
    Guid RepartidorId,
    double Latitud,
    double Longitud,
    DateTime TimestampUtc
);

/// <summary>
/// Contrato del cliente de SignalR para la comunicación bidireccional y streaming en tiempo real (Hito 4).
/// </summary>
public interface IRealtimeTrackingService : IAsyncDisposable
{
    /// <summary>
    /// Indica si la conexión con el Hub de SignalR se encuentra actualmente activa.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Estado detallado de la conexión SignalR.
    /// </summary>
    HubConnectionState State { get; }

    /// <summary>
    /// Evento disparado cuando el estado de la conexión cambia (conectado/desconectado/reconectando).
    /// </summary>
    event EventHandler<bool>? ConnectionStateChanged;

    /// <summary>
    /// Inicia la conexión con el Hub de SignalR de forma asíncrona y segura.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Detiene la conexión con el Hub de SignalR.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transmite la ubicación GPS del repartidor hacia el backend mediante SignalR.
    /// </summary>
    Task EnviarUbicacionAsync(
        Guid hojaDeRutaId,
        Guid repartidorId,
        double latitud,
        double longitud,
        CancellationToken cancellationToken = default);
}
