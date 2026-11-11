namespace UltimaMilla.Application.Ports;

/// <summary>
/// Puerto de notificación en tiempo real hacia el tablero operativo del operador (UC-12, RF-29).
/// Las implementaciones acotan la difusión exclusivamente al grupo del <paramref name="operadorId"/> indicado.
/// </summary>
public interface ITableroRealtimeNotifier
{
    Task NotificarCambioRutaAsync(Guid operadorId, Guid hojaDeRutaId, string estado, CancellationToken ct = default);

    Task NotificarHitoParadaAsync(Guid operadorId, Guid hojaDeRutaId, Guid paradaId, string estadoParada, CancellationToken ct = default);

    /// <summary>
    /// Avisa al tablero que un vehículo informó una posición nueva (RF-23, RF-29), para que el mapa se actualice sin recargar la página.
    /// Por defecto no hace nada: los adaptadores sin tiempo real no están obligados a implementarlo.
    /// </summary>
    /// <summary>
    /// Avisa que cambió el estado de un envío (o que se dio de alta) para que las vistas que lo muestran se actualicen sin recargar (RF-12, RF-29).
    /// Se emite al operador dueño y al comercio dueño del envío, nunca a otros.
    /// </summary>
    Task NotificarEnvioCambioEstadoAsync(Guid operadorId, Guid comercioOperadorId, Guid envioId, string codigoSeguimiento, string estado, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>
    /// Avisa a las sesiones de un comercio que cambió la lista de operadores con los que trabaja (alta de un vínculo nuevo, baja o suspensión).
    /// Se emite al comercio (por su identificador, común a todos sus operadores), nunca a otros.
    /// </summary>
    Task NotificarOperadoresDelComercioAsync(Guid comercioId, CancellationToken ct = default)
        => Task.CompletedTask;

    Task NotificarPosicionVehiculoAsync(Guid operadorId, Guid vehiculoId, Guid? hojaDeRutaId, double latitud, double longitud, CancellationToken ct = default)
        => Task.CompletedTask;
}

