using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
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
    DateTime TimestampUtc
);

/// <summary>
/// Hub de SignalR para la supervision y streaming de telemetria de operaciones en tiempo real (Hito 4 / RNF 6.10).
/// Permite recibir coordenadas GPS periodicas desde UltimaMilla.Mobile, persistir la posicion
/// en base de datos para la ultima ubicacion conocida y retransmitirlas al Tablero en Vivo.
/// </summary>
[Authorize]
public class OperacionHub : Hub
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<OperacionHub> _logger;

    public OperacionHub(AppDbContext db, ICurrentTenant currentTenant, ILogger<OperacionHub> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    /// <summary>
    /// Metodo invocado por la app movil para reportar la posicion GPS periodica.
    /// Persiste la posicion en base de datos y la retransmite en tiempo real.
    /// </summary>
    public async Task TransmitirUbicacion(TransmisionUbicacionRequest payload)
    {
        _logger.LogInformation(
            "SignalR Hub: Coordenadas recibidas de Repartidor {RepartidorId} (Ruta {RutaId}): Lat={Lat:F5}, Lng={Lng:F5}",
            payload.RepartidorId, payload.HojaDeRutaId, payload.Latitud, payload.Longitud);

        try
        {
            await PersistirPosicionEnBaseDatosAsync(payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo no bloqueante al persistir posicion GPS recibida por SignalR.");
        }

        // Retransmitir al canal del Backoffice para actualizacion en vivo del mapa
        await Clients.Others.SendAsync("PosicionRepartidorActualizada", payload);
    }

    /// <summary>
    /// Metodo con parametros escalares para clientes que transmitan campos primitivos directos.
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
    /// Registra la conexion del repartidor en su grupo individual para notificaciones directas desde el Backoffice.
    /// </summary>
    public async Task RegistrarRepartidor(Guid repartidorId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"repartidor_{repartidorId}");
        _logger.LogInformation("SignalR Hub: Repartidor {RepartidorId} suscrito a su canal personal.", repartidorId);
    }

    /// <summary>
    /// Permite a los despachadores del Backoffice suscribirse a los eventos de su operador especifico.
    /// </summary>
    public async Task SuscribirAOperador(string operadorId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"operador-{operadorId}");
        _logger.LogInformation("SignalR Hub: Conexion {ConnectionId} unida al grupo operador {OperadorId}", Context.ConnectionId, operadorId);
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

    private async Task PersistirPosicionEnBaseDatosAsync(TransmisionUbicacionRequest payload)
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

        if (operadorId == Guid.Empty || !vehiculoId.HasValue || vehiculoId.Value == Guid.Empty)
        {
            _logger.LogDebug("No se pudo determinar el vehiculo u operador para persistir la telemetria en tiempo real.");
            return;
        }

        if (_currentTenant is CurrentTenant ct)
        {
            ct.EstablecerOperadorDeProceso(operadorId);
        }

        var ahora = DateTime.UtcNow;
        var tomadaEn = payload.TimestampUtc <= ahora ? payload.TimestampUtc : ahora;

        var posicion = new PosicionVehiculo(
            Guid.NewGuid(),
            operadorId,
            vehiculoId.Value,
            payload.Latitud,
            payload.Longitud,
            "SignalR",
            payload.HojaDeRutaId != Guid.Empty ? payload.HojaDeRutaId : null,
            payload.RepartidorId != Guid.Empty ? payload.RepartidorId : null,
            velocidadKmh: null,
            bateriaPct: null,
            tomadaEn: tomadaEn);

        _db.PosicionesVehiculo.Add(posicion);
        await _db.SaveChangesAsync();
    }
}
