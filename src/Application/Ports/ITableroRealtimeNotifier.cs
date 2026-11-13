namespace UltimaMilla.Application.Ports;

/// <summary>
/// Puerto de notificación en tiempo real hacia el tablero operativo del operador.
/// Las implementaciones acotan la difusión exclusivamente al grupo del <paramref name="operadorId"/> indicado.
/// </summary>
public interface ITableroRealtimeNotifier
{
    Task NotificarCambioRutaAsync(Guid operadorId, Guid hojaDeRutaId, string estado, CancellationToken ct = default);

    Task NotificarHitoParadaAsync(Guid operadorId, Guid hojaDeRutaId, Guid paradaId, string estadoParada, CancellationToken ct = default);

    /// <summary>
    /// Avisa al tablero que un vehículo informó una posición nueva, para que el mapa se actualice sin recargar la página.
    /// Por defecto no hace nada: los adaptadores sin tiempo real no están obligados a implementarlo.
    /// </summary>
    /// <summary>
    /// Avisa que cambió el estado de un envío (o que se dio de alta) para que las vistas que lo muestran se actualicen sin recargar.
    /// Se emite al operador dueño y al comercio dueño del envío, nunca a otros.
    /// </summary>
    Task NotificarEnvioCambioEstadoAsync(Guid operadorId, Guid comercioOperadorId, Guid envioId, string codigoSeguimiento, string estado, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>
    /// Avisa que cambió el estado de un retiro en domicilio (o que se dio de alta) para que las vistas que lo muestran se actualicen sin recargar.
    /// Un retiro no es un envío: tiene su propio evento. Se emite al operador dueño y al comercio dueño, nunca a otros.
    /// </summary>
    Task NotificarRetiroCambioEstadoAsync(Guid operadorId, Guid comercioOperadorId, Guid retiroId, string codigoSeguimiento, string estado, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>Una incidencia se abrió o cambió de estado: solo el personal del operador la ve, así que el aviso va a su grupo.</summary>
    Task NotificarIncidenciaCambioAsync(Guid operadorId, Guid incidenciaId, string tipo, string severidad, string estado, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>
    /// Avisa a las sesiones de un comercio que cambió la lista de operadores con los que trabaja (alta de un vínculo nuevo, baja o suspensión).
    /// Se emite al comercio (por su identificador, común a todos sus operadores), nunca a otros.
    /// </summary>
    Task NotificarOperadoresDelComercioAsync(Guid comercioId, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>
    /// Avisa en tiempo real a todas las sesiones vinculadas al operador (Portal de Comercio y Backoffice) que cambió su identidad visual.
    /// </summary>
    Task NotificarIdentidadVisualActualizadaAsync(Guid operadorId, CancellationToken ct = default)
        => Task.CompletedTask;

    Task NotificarPosicionVehiculoAsync(Guid operadorId, Guid vehiculoId, Guid? hojaDeRutaId, double latitud, double longitud, CancellationToken ct = default)
        => Task.CompletedTask;
}

