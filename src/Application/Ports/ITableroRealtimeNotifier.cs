namespace UltimaMilla.Application.Ports;

/// <summary>
/// Puerto de notificación en tiempo real hacia el tablero operativo del operador (UC-12, RF-29).
/// Las implementaciones acotan la difusión exclusivamente al grupo del <paramref name="operadorId"/> indicado.
/// </summary>
public interface ITableroRealtimeNotifier
{
    Task NotificarCambioRutaAsync(Guid operadorId, Guid hojaDeRutaId, string estado, CancellationToken ct = default);

    Task NotificarHitoParadaAsync(Guid operadorId, Guid hojaDeRutaId, Guid paradaId, string estadoParada, CancellationToken ct = default);
}

