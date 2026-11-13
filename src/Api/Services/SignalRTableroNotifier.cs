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
    public const string EventoEnvioCambioEstado = "EnvioCambioEstado";
    public const string EventoRetiroCambioEstado = "RetiroCambioEstado";
    public const string EventoIncidenciaCambio = "IncidenciaCambio";
    public const string EventoOperadoresDelComercio = "OperadoresDelComercioCambiaron";
    public const string EventoIdentidadVisualActualizada = "IdentidadVisualActualizada";
    public const string EventoSeguimientoActualizado = "EnvioActualizado";

    private readonly IHubContext<TableroHub> _hubContext;
    private readonly IHubContext<SeguimientoHub> _seguimientoHub;
    private readonly ILogger<SignalRTableroNotifier> _logger;

    public SignalRTableroNotifier(IHubContext<TableroHub> hubContext, IHubContext<SeguimientoHub> seguimientoHub, ILogger<SignalRTableroNotifier> logger)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        ArgumentNullException.ThrowIfNull(logger);
        _hubContext = hubContext;
        _seguimientoHub = seguimientoHub;
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

    public async Task NotificarOperadoresDelComercioAsync(Guid comercioId, CancellationToken ct = default)
    {
        if (comercioId == Guid.Empty)
            return;

        try
        {
            await _hubContext.Clients.Group(TableroHub.GrupoCuentaComercio(comercioId))
                .SendAsync(EventoOperadoresDelComercio, new { timestamp = DateTime.UtcNow }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalRTableroNotifier: fallo al emitir {Evento} del comercio {ComercioId}.", EventoOperadoresDelComercio, comercioId);
        }
    }

    public async Task NotificarIdentidadVisualActualizadaAsync(Guid operadorId, CancellationToken ct = default)
    {
        if (operadorId == Guid.Empty)
            return;

        try
        {
            var grupos = new[]
            {
                TableroHub.GrupoIdentidadOperador(operadorId),
                TableroHub.GrupoOperador(operadorId)
            };
            var payload = new { operadorId, timestamp = DateTime.UtcNow };
            await _hubContext.Clients.Groups(grupos).SendAsync(EventoIdentidadVisualActualizada, payload, ct);
            await _hubContext.Clients.Groups(grupos).SendAsync(EventoOperadoresDelComercio, payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalRTableroNotifier: fallo al emitir {Evento} del operador {OperadorId}.", EventoIdentidadVisualActualizada, operadorId);
        }
    }

    public async Task NotificarEnvioCambioEstadoAsync(Guid operadorId, Guid comercioOperadorId, Guid envioId, string codigoSeguimiento, string estado, CancellationToken ct = default)
    {
        if (operadorId == Guid.Empty || envioId == Guid.Empty || string.IsNullOrWhiteSpace(estado))
            return;

        try
        {
            var mensaje = new
            {
                envioId,
                codigoSeguimiento,
                estado = estado.Trim(),
                timestamp = DateTime.UtcNow
            };

            // Operador (backoffice) y comercio dueño del envío: ningún otro grupo lo recibe
            var destinos = new List<string> { TableroHub.GrupoOperador(operadorId) };
            if (comercioOperadorId != Guid.Empty)
                destinos.Add(TableroHub.GrupoComercio(comercioOperadorId));

            await _hubContext.Clients.Groups(destinos).SendAsync(EventoEnvioCambioEstado, mensaje, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalRTableroNotifier: fallo al emitir {Evento} del envío {EnvioId} (operador {OperadorId}).", EventoEnvioCambioEstado, envioId, operadorId);
        }

        await NotificarSeguimientoPublicoAsync(codigoSeguimiento, envioId, ct);
    }

    public async Task NotificarRetiroCambioEstadoAsync(Guid operadorId, Guid comercioOperadorId, Guid retiroId, string codigoSeguimiento, string estado, CancellationToken ct = default)
    {
        if (operadorId == Guid.Empty || retiroId == Guid.Empty || string.IsNullOrWhiteSpace(estado))
            return;

        try
        {
            var mensaje = new
            {
                retiroId,
                codigoSeguimiento,
                estado = estado.Trim(),
                timestamp = DateTime.UtcNow
            };

            // Operador (backoffice) y comercio dueño del retiro: ningún otro grupo lo recibe
            var destinos = new List<string> { TableroHub.GrupoOperador(operadorId) };
            if (comercioOperadorId != Guid.Empty)
                destinos.Add(TableroHub.GrupoComercio(comercioOperadorId));

            await _hubContext.Clients.Groups(destinos).SendAsync(EventoRetiroCambioEstado, mensaje, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalRTableroNotifier: fallo al emitir {Evento} del retiro {RetiroId} (operador {OperadorId}).", EventoRetiroCambioEstado, retiroId, operadorId);
        }
    }

    public async Task NotificarIncidenciaCambioAsync(Guid operadorId, Guid incidenciaId, string tipo, string severidad, string estado, CancellationToken ct = default)
    {
        if (operadorId == Guid.Empty || incidenciaId == Guid.Empty || string.IsNullOrWhiteSpace(estado))
            return;

        try
        {
            // Solo el backoffice del operador: los comercios no ven las incidencias internas
            await _hubContext.Clients.Group(TableroHub.GrupoOperador(operadorId))
                .SendAsync(EventoIncidenciaCambio, new { incidenciaId, tipo, severidad, estado = estado.Trim(), timestamp = DateTime.UtcNow }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalRTableroNotifier: fallo al emitir {Evento} de la incidencia {IncidenciaId} (operador {OperadorId}).", EventoIncidenciaCambio, incidenciaId, operadorId);
        }
    }

    /// <summary>
    /// Señal para quien sigue el envío desde el enlace público. No lleva datos del envío: la página vuelve a consultar el endpoint público,
    /// que aplica las reglas de privacidad.
    /// </summary>
    private async Task NotificarSeguimientoPublicoAsync(string codigoSeguimiento, Guid envioId, CancellationToken ct)
    {
        var codigo = SeguimientoHub.Normalizar(codigoSeguimiento);
        if (codigo is null)
            return;

        try
        {
            await _seguimientoHub.Clients.Group(SeguimientoHub.Grupo(codigo)).SendAsync(EventoSeguimientoActualizado, new { timestamp = DateTime.UtcNow }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalRTableroNotifier: fallo al emitir {Evento} del envío {EnvioId} al seguimiento público.", EventoSeguimientoActualizado, envioId);
        }
    }
}
