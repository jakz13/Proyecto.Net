using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Api.Hubs;
using UltimaMilla.Application.Ports;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Services;

/// <summary>
/// Adaptador SignalR para <see cref="ITableroRealtimeNotifier"/>.
/// Publica eventos en tiempo real únicamente al grupo <c>operador_{operadorId}</c> del inquilino propietario
/// y difunde novedades operativas al canal personal del repartidor en <see cref="OperacionHub"/>.
/// Captura cualquier fallo de transporte para no presentar como fallida una operación ya persistida.
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
    private readonly IHubContext<OperacionHub>? _operacionHub;
    // Se usa un alcance propio para consultar la base: el AppDbContext que dispara este aviso depende de este notificador y, si se lo
    // inyectara directamente, la dependencia sería circular y la API no arrancaría.
    private readonly IServiceScopeFactory? _scopeFactory;

    public SignalRTableroNotifier(
        IHubContext<TableroHub> hubContext,
        IHubContext<SeguimientoHub> seguimientoHub,
        ILogger<SignalRTableroNotifier> logger,
        IHubContext<OperacionHub>? operacionHub = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        ArgumentNullException.ThrowIfNull(logger);
        _hubContext = hubContext;
        _seguimientoHub = seguimientoHub;
        _logger = logger;
        _operacionHub = operacionHub;
        _scopeFactory = scopeFactory;
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

        var estadoNormalizado = estado.Trim();

        try
        {
            var grupo = TableroHub.GrupoOperador(operadorId);
            await _hubContext.Clients.Group(grupo).SendAsync(
                EventoCambioRuta,
                new
                {
                    hojaDeRutaId,
                    estado = estadoNormalizado,
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

        await NotificarRepartidorEnOperacionHubAsync(hojaDeRutaId, estadoNormalizado, ct);
    }

    private async Task NotificarRepartidorEnOperacionHubAsync(Guid hojaDeRutaId, string estado, CancellationToken ct)
    {
        if (_operacionHub is null || _scopeFactory is null || string.Equals(estado, "FlotaActualizada", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            using var alcance = _scopeFactory.CreateScope();
            var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
            var info = await db.HojasDeRuta.IgnoreQueryFilters().AsNoTracking()
                .Where(r => r.Id == hojaDeRutaId)
                .Select(r => new
                {
                    r.RepartidorId,
                    r.Fecha,
                    TotalParadas = r.Paradas.Count(p => p.Estado == "Pendiente" || p.Estado == "EnCamino" || p.Estado == "EnSitio")
                })
                .FirstOrDefaultAsync(ct);

            if (info is null || !info.RepartidorId.HasValue || info.RepartidorId.Value == Guid.Empty)
                return;

            var grupoRepartidor = OperacionHub.GrupoRepartidor(info.RepartidorId.Value);
            var codigoRuta = $"HR-{info.Fecha:yyyyMMdd}-01";
            var payload = new
            {
                hojaDeRutaId,
                codigoRuta,
                estado,
                totalParadas = info.TotalParadas,
                detalleCambio = estado.Equals("Reprogramada", StringComparison.OrdinalIgnoreCase)
                    ? "reprogramacion de horario en una parada"
                    : "actualizacion desde central operativa",
                timestamp = DateTime.UtcNow
            };

            var clientes = _operacionHub.Clients.Group(grupoRepartidor);
            await clientes.SendAsync("CambioEstadoRuta", payload, ct);

            if (estado is "Liberada" or "EnCarga" or "Asignada")
            {
                await clientes.SendAsync("RutaAsignada", payload, ct);
                await clientes.SendAsync("RutaLiberada", payload, ct);
            }
            else if (estado.Equals("Despachada", StringComparison.OrdinalIgnoreCase))
            {
                await clientes.SendAsync("RutaDespachada", payload, ct);
            }
            else if (estado.Equals("EnTransito", StringComparison.OrdinalIgnoreCase) || estado.Equals("Reprogramada", StringComparison.OrdinalIgnoreCase))
            {
                await clientes.SendAsync("RutaActualizada", payload, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "SignalRTableroNotifier: fallo no bloqueante al difundir cambio de ruta {HojaDeRutaId} en OperacionHub.",
                hojaDeRutaId);
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

    public Task NotificarPosicionVehiculoAsync(Guid operadorId, Guid vehiculoId, Guid? hojaDeRutaId, double latitud, double longitud, CancellationToken ct = default)
        => NotificarPosicionVehiculoAsync(operadorId, vehiculoId, hojaDeRutaId, latitud, longitud, null, null, ct);

    public async Task NotificarPosicionVehiculoAsync(Guid operadorId, Guid vehiculoId, Guid? hojaDeRutaId, double latitud, double longitud, double? velocidadKmh, int? bateriaPct, CancellationToken ct = default)
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
                    velocidadKmh = velocidadKmh is >= 0 ? (int?)Math.Round(velocidadKmh.Value) : null,
                    nivelBateriaPct = bateriaPct is > 0 and <= 100 ? bateriaPct : null,
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
