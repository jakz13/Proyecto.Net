using Microsoft.AspNetCore.SignalR;
using UltimaMilla.Api.Hubs;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Api.Services;

/// <summary>
/// Adaptador SignalR para <see cref="ITableroRealtimeNotifier"/>.
/// Publica eventos en tiempo real únicamente al grupo <c>operador_{operadorId}</c> del inquilino propietario
/// y captura cualquier fallo de transporte para no presentar como fallida una operación ya persistida.
/// </summary>
public class SignalRTableroNotifier : ITableroRealtimeNotifier
{
    public const string EventoCambioRuta = "CambioRuta";
    public const string EventoHitoParada = "HitoParada";
    public const string EventoPosicionVehiculo = "PosicionVehiculo";

    private readonly IHubContext<TableroHub> _hubContext;
    private readonly ILogger<SignalRTableroNotifier> _logger;

    public SignalRTableroNotifier(IHubContext<TableroHub> hubContext, ILogger<SignalRTableroNotifier> logger)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        ArgumentNullException.ThrowIfNull(logger);
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotificarCambioRutaAsync(Guid operadorId, Guid hojaDeRutaId, string estado, CancellationToken ct = default)
    {
        if (operadorId == Guid.Empty || hojaDeRutaId == Guid.Empty || string.IsNullOrWhiteSpace(estado))
        {
            _logger.LogWarning(
                "SignalRTableroNotifier: se omitió NotificarCambioRutaAsync por parámetros inválidos (OperadorId={OperadorId}, HojaDeRutaId={HojaDeRutaId}).",
                operadorId,
                hojaDeRutaId);
            return;
        }

        try
        {
            var grupo = TableroHub.GrupoOperador(operadorId);
            await _hubContext.Clients.Group(grupo).SendAsync(
                EventoCambioRuta,
                new
                {
                    hojaDeRutaId,
                    estado = estado.Trim(),
                    timestamp = DateTime.UtcNow
                },
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "SignalRTableroNotifier: fallo al emitir {Evento} para HojaDeRuta {HojaDeRutaId} del operador {OperadorId}.",
                EventoCambioRuta,
                hojaDeRutaId,
                operadorId);
        }
    }

    public async Task NotificarHitoParadaAsync(Guid operadorId, Guid hojaDeRutaId, Guid paradaId, string estadoParada, CancellationToken ct = default)
    {
        if (operadorId == Guid.Empty || hojaDeRutaId == Guid.Empty || paradaId == Guid.Empty || string.IsNullOrWhiteSpace(estadoParada))
        {
            _logger.LogWarning(
                "SignalRTableroNotifier: se omitió NotificarHitoParadaAsync por parámetros inválidos (OperadorId={OperadorId}, HojaDeRutaId={HojaDeRutaId}, ParadaId={ParadaId}).",
                operadorId,
                hojaDeRutaId,
                paradaId);
            return;
        }

        try
        {
            var grupo = TableroHub.GrupoOperador(operadorId);
            await _hubContext.Clients.Group(grupo).SendAsync(
                EventoHitoParada,
                new
                {
                    hojaDeRutaId,
                    paradaId,
                    estadoParada = estadoParada.Trim(),
                    timestamp = DateTime.UtcNow
                },
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "SignalRTableroNotifier: fallo al emitir {Evento} para Parada {ParadaId} (HojaDeRuta {HojaDeRutaId}, Operador {OperadorId}).",
                EventoHitoParada,
                paradaId,
                hojaDeRutaId,
                operadorId);
        }
    }

    public async Task NotificarPosicionVehiculoAsync(Guid operadorId, Guid vehiculoId, Guid? hojaDeRutaId, double latitud, double longitud, CancellationToken ct = default)
    {
        if (operadorId == Guid.Empty || vehiculoId == Guid.Empty || double.IsNaN(latitud) || double.IsNaN(longitud))
            return;

        try
        {
            await _hubContext.Clients.Group(TableroHub.GrupoOperador(operadorId)).SendAsync(
                EventoPosicionVehiculo,
                new
                {
                    vehiculoId,
                    hojaDeRutaId,
                    latitud,
                    longitud,
                    timestamp = DateTime.UtcNow
                },
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalRTableroNotifier: fallo al emitir {Evento} del vehículo {VehiculoId} (operador {OperadorId}).", EventoPosicionVehiculo, vehiculoId, operadorId);
        }
    }
}
