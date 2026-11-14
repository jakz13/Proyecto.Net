using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Api.Auth;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Services;

namespace UltimaMilla.Api.Hubs;

/// <summary>
/// DTO con los datos de posicion transmitidos desde la app movil.
/// </summary>
public record TransmisionUbicacionRequest(
    Guid HojaDeRutaId,
    Guid RepartidorId,
    double Latitud,
    double Longitud,
    DateTime TimestampUtc,
    double? VelocidadKmh = null,
    int? BateriaPct = null
);

/// <summary>
/// Hub de SignalR para la supervision y streaming de telemetria de operaciones en tiempo real.
/// Permite recibir coordenadas GPS periodicas desde UltimaMilla.Mobile, persistir la posicion
/// en base de datos para la ultima ubicacion conocida y retransmitirlas al Tablero en Vivo.
/// </summary>
[Authorize]
public class OperacionHub : Hub
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<OperacionHub> _logger;
    private readonly ITableroRealtimeNotifier? _tableroNotifier;
    private readonly IHubContext<TableroHub>? _tableroHubContext;

    public OperacionHub(
        AppDbContext db,
        ICurrentTenant currentTenant,
        ILogger<OperacionHub> logger,
        ITableroRealtimeNotifier? tableroNotifier = null,
        IHubContext<TableroHub>? tableroHubContext = null)
    {
        _db = db;
        _currentTenant = currentTenant;
        _logger = logger;
        _tableroNotifier = tableroNotifier;
        _tableroHubContext = tableroHubContext;
    }

    /// <summary>
    /// Metodo invocado por la app movil para reportar la posicion GPS periodica.
    /// Persiste la posicion en base de datos y la retransmite en tiempo real.
    /// </summary>
    [Authorize(Policy = PoliticasApi.Repartidor)]
    public async Task TransmitirUbicacion(TransmisionUbicacionRequest payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        // El operador y el repartidor salen del token firmado, nunca del cuerpo: antes cualquier usuario autenticado (incluso de un comercio)
        // podía enviar la posición de cualquier repartidor y la recibían todas las conexiones del hub, de cualquier operador (D-13, auditoría del 14/11)
        var (operadorToken, repartidorToken) = await ResolverRepartidorDelTokenAsync();
        var rutaPropia = payload.HojaDeRutaId != Guid.Empty && await _db.HojasDeRuta.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(r => r.Id == payload.HojaDeRutaId && r.RepartidorId == repartidorToken && r.OperadorId == operadorToken);
        payload = payload with { RepartidorId = repartidorToken, HojaDeRutaId = rutaPropia ? payload.HojaDeRutaId : Guid.Empty };

        _logger.LogInformation(
            "SignalR Hub: Coordenadas recibidas de Repartidor {RepartidorId} (Ruta {RutaId}): Lat={Lat:F5}, Lng={Lng:F5}",
            payload.RepartidorId, payload.HojaDeRutaId, payload.Latitud, payload.Longitud);

        (Guid OperadorId, Guid? VehiculoId) resolucion = (Guid.Empty, null);
        try
        {
            resolucion = await PersistirPosicionEnBaseDatosAsync(payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo no bloqueante al persistir posicion GPS recibida por SignalR.");
        }

        // 1. Retransmitir solo a las conexiones del mismo operador (antes: a todas las del hub, de cualquier operador)
        await Clients.OthersInGroup($"operador-{operadorToken}").SendAsync("PosicionRepartidorActualizada", payload);

        // 2. Puente obligatorio hacia TableroHub para que el Backoffice reciba las coordenadas en vivo
        if (resolucion.OperadorId != Guid.Empty && resolucion.VehiculoId.HasValue && resolucion.VehiculoId.Value != Guid.Empty)
        {
            if (_tableroNotifier != null)
            {
                await _tableroNotifier.NotificarPosicionVehiculoAsync(
                    resolucion.OperadorId,
                    resolucion.VehiculoId.Value,
                    payload.HojaDeRutaId != Guid.Empty ? payload.HojaDeRutaId : null,
                    payload.Latitud,
                    payload.Longitud,
                    payload.VelocidadKmh,
                    payload.BateriaPct);
            }

            if (_tableroHubContext != null)
            {
                await _tableroHubContext.Clients.Group(TableroHub.GrupoOperador(resolucion.OperadorId)).SendAsync(
                    "PosicionRepartidorActualizada",
                    new
                    {
                        repartidorId = payload.RepartidorId,
                        hojaDeRutaId = payload.HojaDeRutaId,
                        vehiculoId = resolucion.VehiculoId.Value,
                        latitud = payload.Latitud,
                        longitud = payload.Longitud,
                        velocidadKmh = payload.VelocidadKmh is >= 0 ? (int?)Math.Round(payload.VelocidadKmh.Value) : null,
                        nivelBateriaPct = payload.BateriaPct is > 0 and <= 100 ? payload.BateriaPct : null,
                        timestamp = payload.TimestampUtc
                    });
            }
        }
    }

    /// <summary>
    /// Metodo con parametros escalares para clientes que transmitan campos primitivos directos.
    /// </summary>
    [Authorize(Policy = PoliticasApi.Repartidor)]
    public async Task TransmitirCoordenadas(Guid hojaDeRutaId, Guid repartidorId, double latitud, double longitud, double? velocidadKmh = null, int? bateriaPct = null)
    {
        var request = new TransmisionUbicacionRequest(
            hojaDeRutaId,
            repartidorId,
            latitud,
            longitud,
            DateTime.UtcNow,
            velocidadKmh,
            bateriaPct);

        await TransmitirUbicacion(request);
    }

    public static string GrupoRepartidor(Guid repartidorId) => $"repartidor_{repartidorId}";

    /// <summary>
    /// Registra la conexion del repartidor en su grupo individual para notificaciones directas desde el Backoffice.
    /// </summary>
    [Authorize(Policy = PoliticasApi.Repartidor)]
    public async Task RegistrarRepartidor(Guid repartidorId)
    {
        if (repartidorId == Guid.Empty)
            return;

        // Solo el canal propio: antes se podía escuchar el canal personal (novedades de ruta) de cualquier repartidor de cualquier operador
        var (_, repartidorToken) = await ResolverRepartidorDelTokenAsync();
        if (repartidorId != repartidorToken)
            throw new HubException("No puede suscribirse al canal de otro repartidor.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GrupoRepartidor(repartidorId));
        _logger.LogInformation("SignalR Hub: Repartidor {RepartidorId} suscrito a su canal personal.", repartidorId);
    }

    /// <summary>
    /// Permite a los despachadores del Backoffice suscribirse a los eventos de su operador especifico.
    /// </summary>
    public async Task SuscribirAOperador(string operadorId)
    {
        // Solo al operador del token (y nunca un usuario de comercio, que solo ve lo suyo): antes se aceptaba el operador que enviara el cliente
        if (!TableroHub.IntentarExtraerOperadorId(Context.User, out var operadorToken)
            || !Guid.TryParse(operadorId, out var pedido) || pedido != operadorToken
            || TableroHub.EsUsuarioDeComercio(Context.User))
            throw new HubException("No puede suscribirse a los eventos de otro operador.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"operador-{operadorToken}");
        _logger.LogInformation("SignalR Hub: Conexion {ConnectionId} unida al grupo operador {OperadorId}", Context.ConnectionId, operadorToken);
    }

    /// <summary>Operador y repartidor del usuario autenticado, resueltos desde el token y la base; si no es un repartidor de ese operador se rechaza.</summary>
    private async Task<(Guid OperadorId, Guid RepartidorId)> ResolverRepartidorDelTokenAsync()
    {
        if (!TableroHub.IntentarExtraerOperadorId(Context.User, out var operadorId))
            throw new HubException("La sesión no tiene operador.");

        Guid? repartidorId = null;
        if (Guid.TryParse(Context.User?.FindFirst("repartidor_id")?.Value, out var claimId) && claimId != Guid.Empty)
        {
            repartidorId = claimId;
        }
        else
        {
            var claimSub = Context.User?.FindFirst("sub")?.Value
                ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(claimSub, out var usuarioId) && usuarioId != Guid.Empty)
            {
                repartidorId = await _db.Repartidores.IgnoreQueryFilters().AsNoTracking()
                    .Where(r => (r.Id == usuarioId || r.UsuarioId == usuarioId) && r.OperadorId == operadorId)
                    .Select(r => (Guid?)r.Id)
                    .FirstOrDefaultAsync(Context.ConnectionAborted);
            }
        }

        var existe = repartidorId.HasValue && await _db.Repartidores.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(r => r.Id == repartidorId.Value && r.OperadorId == operadorId, Context.ConnectionAborted);
        if (!existe)
            throw new HubException("El usuario no es un repartidor de este operador.");

        return (operadorId, repartidorId!.Value);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR Hub: Cliente conectado. ConnectionId: {ConnectionId}", Context.ConnectionId);

        // Un usuario de comercio no se une al grupo del operador: recibiría la posición de toda la flota (el comercio solo ve lo suyo)
        if (TableroHub.IntentarExtraerOperadorId(Context.User, out var opId) && !TableroHub.EsUsuarioDeComercio(Context.User))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"operador-{opId}");
        }

        try
        {
            var claimRep = Context.User?.FindFirst("repartidor_id")?.Value;
            if (Guid.TryParse(claimRep, out var repId) && repId != Guid.Empty)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GrupoRepartidor(repId));
            }
            else
            {
                var claimSub = Context.User?.FindFirst("sub")?.Value
                    ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (Guid.TryParse(claimSub, out var userId) && userId != Guid.Empty)
                {
                    var rep = await _db.Repartidores.IgnoreQueryFilters().AsNoTracking()
                        .Where(r => r.Id == userId || r.UsuarioId == userId)
                        .Select(r => (Guid?)r.Id)
                        .FirstOrDefaultAsync();

                    if (rep.HasValue && rep.Value != Guid.Empty)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, GrupoRepartidor(rep.Value));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo auto-suscribir al repartidor al conectar OperacionHub.");
        }

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(exception, "SignalR Hub: Cliente desconectado. ConnectionId: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    private async Task<(Guid OperadorId, Guid? VehiculoId)> PersistirPosicionEnBaseDatosAsync(TransmisionUbicacionRequest payload)
    {
        Guid operadorId = Guid.Empty;
        Guid? vehiculoId = null;

        if (TableroHub.IntentarExtraerOperadorId(Context.User, out var opId))
        {
            operadorId = opId;
        }

        if (payload.HojaDeRutaId != Guid.Empty)
        {
            var ruta = await _db.HojasDeRuta.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == payload.HojaDeRutaId);

            if (ruta != null)
            {
                if (operadorId == Guid.Empty)
                    operadorId = ruta.OperadorId;

                vehiculoId = ruta.VehiculoId;
            }
        }

        if ((!vehiculoId.HasValue || vehiculoId.Value == Guid.Empty) && payload.RepartidorId != Guid.Empty)
        {
            var repartidor = await _db.Repartidores.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == payload.RepartidorId);

            if (repartidor != null)
            {
                if (operadorId == Guid.Empty)
                    operadorId = repartidor.OperadorId;

                vehiculoId = repartidor.VehiculoAsignadoId;
            }
        }

        // Si aún no se determinó el operador, intentar resolverlo por el repartidor autenticado
        if (operadorId == Guid.Empty && payload.RepartidorId != Guid.Empty)
        {
            var rep = await _db.Repartidores.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == payload.RepartidorId);
            if (rep != null)
            {
                operadorId = rep.OperadorId;
            }
        }

        // Fallback de vehículo: si el operador está identificado pero ni la ruta ni el repartidor tienen vehículo asignado,
        // resolver el primer vehículo activo del operador para no descartar la telemetría en tiempo real
        if (operadorId != Guid.Empty && (!vehiculoId.HasValue || vehiculoId.Value == Guid.Empty))
        {
            var vehiculoFallback = await _db.Vehiculos.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(v => v.OperadorId == operadorId && v.Activo);
            if (vehiculoFallback != null)
            {
                vehiculoId = vehiculoFallback.Id;
            }
        }

        if (operadorId == Guid.Empty || !vehiculoId.HasValue || vehiculoId.Value == Guid.Empty)
        {
            _logger.LogDebug("No se pudo determinar el vehiculo u operador para persistir la telemetria en tiempo real.");
            return (operadorId, vehiculoId);
        }

        if (_currentTenant is CurrentTenant ct)
        {
            ct.EstablecerOperadorDeProceso(operadorId);
        }

        var ahora = DateTime.UtcNow;
        var tomadaEn = payload.TimestampUtc <= ahora ? payload.TimestampUtc : ahora;

        int? bateriaReal = payload.BateriaPct is > 0 and <= 100 ? payload.BateriaPct : null;
        double? velocidadReal = payload.VelocidadKmh is >= 0 and <= 300 ? payload.VelocidadKmh : null;

        if (!bateriaReal.HasValue)
        {
            bateriaReal = await _db.PosicionesVehiculo.IgnoreQueryFilters().AsNoTracking()
                .Where(p => p.VehiculoId == vehiculoId.Value
                            && p.Origen == "Movil"
                            && p.BateriaPct != null
                            && p.Timestamp > ahora.AddHours(-6))
                .OrderByDescending(p => p.Timestamp)
                .Select(p => p.BateriaPct)
                .FirstOrDefaultAsync();
        }

        var posicion = new PosicionVehiculo(
            Guid.NewGuid(),
            operadorId,
            vehiculoId.Value,
            payload.Latitud,
            payload.Longitud,
            "SignalR",
            payload.HojaDeRutaId != Guid.Empty ? payload.HojaDeRutaId : null,
            payload.RepartidorId != Guid.Empty ? payload.RepartidorId : null,
            velocidadKmh: velocidadReal,
            bateriaPct: bateriaReal,
            tomadaEn: tomadaEn);

        _db.PosicionesVehiculo.Add(posicion);
        await _db.SaveChangesAsync();

        return (operadorId, vehiculoId);
    }
}
