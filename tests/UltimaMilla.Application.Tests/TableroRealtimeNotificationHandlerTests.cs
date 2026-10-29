using UltimaMilla.Application.Commands.Deposito.MarcarEnvioExtraviado;
using UltimaMilla.Application.Commands.Deposito.RendirHojaDeRuta;
using UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarEntregaExitosa;
using UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarIntentoFallido;
using UltimaMilla.Application.Commands.Mobile.Navegacion.SeleccionarParadaActual;
using UltimaMilla.Application.Commands.Planificacion.DespacharHojaDeRuta;
using UltimaMilla.Application.Commands.Planificacion.ReasignarEnvio;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Tests;

/// <summary>
/// Verifica la integración de <see cref="ITableroRealtimeNotifier"/> en los comandos que mutan hojas de ruta y paradas:
/// emisión posterior a <see cref="IUnitOfWork.SaveChangesAsync"/>, ausencia de duplicados en operaciones idempotentes
/// y resiliencia frente a fallos del transporte SignalR sin revertir la operación ya persistida.
/// </summary>
public class TableroRealtimeNotificationHandlerTests
{
    private static readonly DateOnly FechaOperacion = new(2026, 10, 16);

    private readonly FakeRutaRepository _rutas = new();
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeRepartidorRepository _repartidores = new();
    private readonly FakeVehiculoRepository _vehiculos = new();
    private readonly FakeFranjaHorariaRepository _franjas = new();
    private readonly FakeRendicionRepository _rendiciones = new();
    private readonly FakeOutboxStorage _outbox = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();
    private readonly FakeCacheService _cache = new();
    private readonly FakeTableroRealtimeNotifier _notifier;
    private readonly Repartidor _repartidor;
    private readonly Vehiculo _vehiculo;
    private readonly MotivoNoEntrega _motivoCalle;
    private readonly MotivoNoEntrega _motivoInterno;
    private int _secuencia;

    public TableroRealtimeNotificationHandlerTests()
    {
        _notifier = new FakeTableroRealtimeNotifier(_uow);
        _repartidor = new Repartidor(Guid.NewGuid(), _tenant.UsuarioId!.Value, _tenant.OperadorId, "1.111.111-1", "099111111");
        _vehiculo = new Vehiculo(Guid.NewGuid(), _tenant.OperadorId, "SAB1234", "Furgoneta", 500m, 100000m);
        _repartidores.Repartidores.Add(_repartidor);
        _vehiculos.Vehiculos.Add(_vehiculo);

        _envios.ReglaOperativa = new ReglaOperativa(
            Guid.NewGuid(),
            _tenant.OperadorId,
            1,
            _clock.UtcNow.AddDays(-5),
            3,
            TimeSpan.FromHours(24),
            "Firma",
            "Devolución",
            TimeSpan.FromHours(48),
            TimeSpan.FromHours(24),
            null,
            3);

        _motivoCalle = new MotivoNoEntrega(Guid.NewGuid(), _tenant.OperadorId, "AUSENTE", "Destinatario ausente");
        _motivoInterno = new MotivoNoEntrega(Guid.NewGuid(), _tenant.OperadorId, "RETORNO_SIN_INTENTO", "Retorno sin intento", esInterno: true);
        _envios.Motivos.Add(_motivoCalle);
        _envios.Motivos.Add(_motivoInterno);
    }

    private Envio CrearEnvioClasificado()
    {
        _secuencia++;
        var codigo = $"TRK-RT{_secuencia:0000}";
        var envio = new Envio(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Estandar",
            codigo,
            direccionEntregaId: Guid.NewGuid(),
            operadorId: _tenant.OperadorId);

        var idBulto = $"{codigo}-B01";
        envio.AgregarBulto(idBulto, 2m, 10m, 10m, 10m);
        envio.RecepcionarEnDeposito(new[] { idBulto }, null, null, Guid.NewGuid());
        envio.ClasificarBultos(new[] { new ClasificacionBulto(idBulto, PrioridadBulto.Normal) }, Guid.NewGuid());
        _envios.Envios.Add(envio);
        return envio;
    }

    private HojaDeRuta CrearRutaEnEstado(string estado, params Envio[] envios)
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, FechaOperacion);
        ruta.AsignarRecursos(_repartidor.Id, _vehiculo.Id);
        var orden = 1;
        foreach (var envio in envios)
        {
            ruta.AgregarParada(envio.Id, orden++, envio.DireccionEntregaId!.Value);
            ruta.RegistrarAsignacion(envio.Id, Guid.NewGuid());
            envio.AsignarRuta(ruta.Id);
        }

        ruta.Liberar();
        if (estado is "EnCarga" or "Despachada")
        {
            ruta.RegistrarCargaEscaneada(envios.SelectMany(e => e.Bultos).Select(b => b.Id));
        }

        if (estado == "Despachada")
        {
            ruta.Despachar();
            foreach (var envio in envios)
            {
                envio.DespacharEnRuta(ruta.Id, Guid.NewGuid());
            }
        }

        _rutas.Rutas.Add(ruta);
        return ruta;
    }

    // ---- 1. DespacharHojaDeRutaCommandHandler ----

    [Fact]
    public async Task DespacharRuta_NotificaCambioRutaDespuesDeGuardar_YNoDuplicaEnReintentoIdempotente()
    {
        var envio = CrearEnvioClasificado();
        var ruta = CrearRutaEnEstado("EnCarga", envio);
        var handler = new DespacharHojaDeRutaCommandHandler(_rutas, _envios, _outbox, _uow, _tenant, _clock, _notifier);

        var resultado = await handler.Handle(new DespacharHojaDeRutaCommand(ruta.Id, "Salida normal"), CancellationToken.None);

        Assert.False(resultado.Idempotente);
        var notif = Assert.Single(_notifier.CambiosRuta);
        Assert.Equal(_tenant.OperadorId, notif.OperadorId);
        Assert.Equal(ruta.Id, notif.HojaDeRutaId);
        Assert.Equal("Despachada", notif.Estado);
        Assert.Equal(1, notif.ConfirmacionesEnMomentoDeNotificar);

        // Reintento idempotente: no debe volver a notificar
        var reintento = await handler.Handle(new DespacharHojaDeRutaCommand(ruta.Id, "Salida normal"), CancellationToken.None);
        Assert.True(reintento.Idempotente);
        Assert.Single(_notifier.CambiosRuta);
    }

    [Fact]
    public async Task DespacharRuta_CuandoSignalRFalla_NoRevierteElDespachoPersistido()
    {
        var envio = CrearEnvioClasificado();
        var ruta = CrearRutaEnEstado("EnCarga", envio);
        _notifier.LanzarExcepcion = true;
        var handler = new DespacharHojaDeRutaCommandHandler(_rutas, _envios, _outbox, _uow, _tenant, _clock, _notifier);

        var resultado = await handler.Handle(new DespacharHojaDeRutaCommand(ruta.Id, null), CancellationToken.None);

        Assert.Equal("Despachada", resultado.Estado);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    // ---- 2. RendirHojaDeRutaCommandHandler ----

    [Fact]
    public async Task RendirRuta_NotificaCambioRutaDespuesDeGuardar_YNoDuplicaEnReintentoIdempotente()
    {
        var envio = CrearEnvioClasificado();
        var ruta = CrearRutaEnEstado("Despachada", envio);
        var codigosBultos = envio.Bultos.Select(b => b.Identificacion).ToList();
        var handler = new RendirHojaDeRutaCommandHandler(_rutas, _envios, _rendiciones, _outbox, _uow, _tenant, _clock, _notifier);

        var resultado = await handler.Handle(
            new RendirHojaDeRutaCommand(ruta.Id, codigosBultos, "RETORNO_SIN_INTENTO", "Cierre turno"),
            CancellationToken.None);

        Assert.False(resultado.Idempotente);
        var notif = Assert.Single(_notifier.CambiosRuta);
        Assert.Equal(_tenant.OperadorId, notif.OperadorId);
        Assert.Equal(ruta.Id, notif.HojaDeRutaId);
        Assert.Equal("Rendida", notif.Estado);
        Assert.Equal(1, notif.ConfirmacionesEnMomentoDeNotificar);

        // Reintento idempotente
        var reintento = await handler.Handle(
            new RendirHojaDeRutaCommand(ruta.Id, codigosBultos, "RETORNO_SIN_INTENTO", "Cierre turno"),
            CancellationToken.None);
        Assert.True(reintento.Idempotente);
        Assert.Single(_notifier.CambiosRuta);
    }

    [Fact]
    public async Task RendirRuta_CuandoSignalRFalla_NoFallaLaRendicionPersistida()
    {
        var envio = CrearEnvioClasificado();
        var ruta = CrearRutaEnEstado("Despachada", envio);
        var codigosBultos = envio.Bultos.Select(b => b.Identificacion).ToList();
        _notifier.LanzarExcepcion = true;
        var handler = new RendirHojaDeRutaCommandHandler(_rutas, _envios, _rendiciones, _outbox, _uow, _tenant, _clock, _notifier);

        var resultado = await handler.Handle(
            new RendirHojaDeRutaCommand(ruta.Id, codigosBultos, "RETORNO_SIN_INTENTO", null),
            CancellationToken.None);

        Assert.Equal("Rendida", resultado.EstadoRuta);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    // ---- 3. SeleccionarParadaActualCommandHandler ----

    [Fact]
    public async Task SeleccionarParadaActual_NotificaHitoParadaDespuesDeGuardar_YEsResilienteAnteFalloSignalR()
    {
        var envio = CrearEnvioClasificado();
        var ruta = CrearRutaEnEstado("Despachada", envio);
        var parada = ruta.Paradas.Single();
        var handler = new SeleccionarParadaActualCommandHandler(_rutas, _uow, _outbox, _tenant, _clock, _notifier);

        var resultado = await handler.Handle(
            new SeleccionarParadaActualCommand(ruta.Id, parada.Id, -34.9011, -56.1645, _clock.UtcNow, "IniciarNavegacion"),
            CancellationToken.None);

        Assert.Equal("EnCamino", resultado.NuevoEstado);
        var hito = Assert.Single(_notifier.HitosParada);
        Assert.Equal(_tenant.OperadorId, hito.OperadorId);
        Assert.Equal(ruta.Id, hito.HojaDeRutaId);
        Assert.Equal(parada.Id, hito.ParadaId);
        Assert.Equal("EnCamino", hito.EstadoParada);
        Assert.Equal(1, hito.ConfirmacionesEnMomentoDeNotificar);

        // Si SignalR falla al registrar el arribo, el cambio de estado se mantiene persistido
        _notifier.LanzarExcepcion = true;
        var arribo = await handler.Handle(
            new SeleccionarParadaActualCommand(ruta.Id, parada.Id, -34.9011, -56.1645, _clock.UtcNow, "RegistrarArribo"),
            CancellationToken.None);
        Assert.Equal("EnSitio", arribo.NuevoEstado);
        Assert.Equal(2, _uow.Confirmaciones);
    }

    // ---- 4. RegistrarEntregaExitosaCommandHandler ----

    [Fact]
    public async Task RegistrarEntregaExitosa_NotificaHitoParadaYFinalizacionDeRutaDespuesDeGuardar()
    {
        var envio = CrearEnvioClasificado();
        var ruta = CrearRutaEnEstado("Despachada", envio);
        var parada = ruta.Paradas.Single();
        var handler = new RegistrarEntregaExitosaCommandHandler(_envios, _rutas, _uow, _outbox, _cache, _tenant, _clock, _notifier);

        var cmd = new RegistrarEntregaExitosaCommand(
            ruta.Id,
            parada.Id,
            envio.Id,
            "idem-entrega-1",
            "María Pérez",
            "4.123.456-7",
            "data:image/png;base64,iVBORw0KGgo=",
            null,
            -34.9011,
            -56.1645,
            _clock.UtcNow);

        var resultado = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(resultado.Idempotente);
        var hito = Assert.Single(_notifier.HitosParada);
        Assert.Equal(_tenant.OperadorId, hito.OperadorId);
        Assert.Equal(ruta.Id, hito.HojaDeRutaId);
        Assert.Equal(parada.Id, hito.ParadaId);
        Assert.Equal("Entregada", hito.EstadoParada);
        Assert.Equal(1, hito.ConfirmacionesEnMomentoDeNotificar);

        var cambioRuta = Assert.Single(_notifier.CambiosRuta);
        Assert.Equal("Finalizada", cambioRuta.Estado);

        // Reintento idempotente con la misma clave no debe volver a emitir SignalR
        var reintento = await handler.Handle(cmd, CancellationToken.None);
        Assert.True(reintento.Idempotente);
        Assert.Single(_notifier.HitosParada);
        Assert.Single(_notifier.CambiosRuta);
    }

    [Fact]
    public async Task RegistrarEntregaExitosa_CuandoSignalRFalla_NoRevierteLaEntregaPersistida()
    {
        var envio = CrearEnvioClasificado();
        var ruta = CrearRutaEnEstado("Despachada", envio);
        var parada = ruta.Paradas.Single();
        _notifier.LanzarExcepcion = true;
        var handler = new RegistrarEntregaExitosaCommandHandler(_envios, _rutas, _uow, _outbox, _cache, _tenant, _clock, _notifier);

        var cmd = new RegistrarEntregaExitosaCommand(
            ruta.Id,
            parada.Id,
            envio.Id,
            "idem-entrega-resiliente",
            "María Pérez",
            "4.123.456-7",
            "data:image/png;base64,iVBORw0KGgo=",
            null,
            -34.9011,
            -56.1645,
            _clock.UtcNow);

        var resultado = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("Entregado", resultado.EstadoEnvio);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    // ---- 5. RegistrarIntentoFallidoCommandHandler ----

    [Fact]
    public async Task RegistrarIntentoFallido_NotificaHitoParadaNoEntregadaDespuesDeGuardar()
    {
        var envio1 = CrearEnvioClasificado();
        var envio2 = CrearEnvioClasificado();
        var ruta = CrearRutaEnEstado("Despachada", envio1, envio2);
        var parada1 = ruta.Paradas.First(p => p.EnvioId == envio1.Id);
        var handler = new RegistrarIntentoFallidoCommandHandler(
            _envios, _rutas, _uow, _outbox, _cache, _tenant, _clock, _notifier);

        var cmd = new RegistrarIntentoFallidoCommand(
            ruta.Id,
            parada1.Id,
            envio1.Id,
            "idem-fallo-1",
            _motivoCalle.Id,
            "AUSENTE",
            "No responde timbre",
            null,
            -34.9011,
            -56.1645,
            _clock.UtcNow);

        var resultado = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(resultado.Idempotente);
        var hito = Assert.Single(_notifier.HitosParada);
        Assert.Equal(_tenant.OperadorId, hito.OperadorId);
        Assert.Equal(ruta.Id, hito.HojaDeRutaId);
        Assert.Equal(parada1.Id, hito.ParadaId);
        Assert.Equal("Fallida", hito.EstadoParada);
        Assert.Equal(1, hito.ConfirmacionesEnMomentoDeNotificar);

        // Reintento idempotente
        var reintento = await handler.Handle(cmd, CancellationToken.None);
        Assert.True(reintento.Idempotente);
        Assert.Single(_notifier.HitosParada);
    }

    // ---- 6. ReasignarEnvioCommandHandler & MarcarEnvioExtraviadoCommandHandler ----

    [Fact]
    public async Task ReasignarEnvio_EnRutaDespachada_NotificaCambioRutaDespuesDeGuardar()
    {
        var envio1 = CrearEnvioClasificado();
        var envio2 = CrearEnvioClasificado();
        var rutaOrigen = CrearRutaEnEstado("Despachada", envio1, envio2);
        var handler = new ReasignarEnvioCommandHandler(
            _rutas, _envios, _vehiculos, _franjas, _outbox, _uow, _tenant, _clock, _notifier);

        var resultado = await handler.Handle(
            new ReasignarEnvioCommand(rutaOrigen.Id, envio1.Id, "Retirar", null, null, "RETORNO_SIN_INTENTO", "Cambio de ruta"),
            CancellationToken.None);

        Assert.Equal("Retirar", resultado.Accion);
        var notif = Assert.Single(_notifier.CambiosRuta);
        Assert.Equal(_tenant.OperadorId, notif.OperadorId);
        Assert.Equal(rutaOrigen.Id, notif.HojaDeRutaId);
        Assert.Equal("Despachada", notif.Estado);
        Assert.Equal(1, notif.ConfirmacionesEnMomentoDeNotificar);
    }

    [Fact]
    public async Task MarcarEnvioExtraviado_EnRutaActiva_NotificaCambioRutaDespuesDeGuardar()
    {
        var envio = CrearEnvioClasificado();
        var ruta = CrearRutaEnEstado("Liberada", envio);
        var handler = new MarcarEnvioExtraviadoCommandHandler(_envios, _rutas, _outbox, _uow, _tenant, _notifier);

        var resultado = await handler.Handle(
            new MarcarEnvioExtraviadoCommand(envio.Id, "Extraviado en sector de carga"),
            CancellationToken.None);

        Assert.True(resultado.RetiradoDeRuta);
        var notif = Assert.Single(_notifier.CambiosRuta);
        Assert.Equal(_tenant.OperadorId, notif.OperadorId);
        Assert.Equal(ruta.Id, notif.HojaDeRutaId);
        Assert.Equal("Liberada", notif.Estado);
        Assert.Equal(1, notif.ConfirmacionesEnMomentoDeNotificar);
    }

    private sealed class FakeCacheService : IDistributedCacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<T?>(default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeTableroRealtimeNotifier : ITableroRealtimeNotifier
    {
        private readonly FakeUnitOfWork _uow;

        public FakeTableroRealtimeNotifier(FakeUnitOfWork uow)
        {
            _uow = uow;
        }

        public bool LanzarExcepcion { get; set; }

        public List<(Guid OperadorId, Guid HojaDeRutaId, string Estado, int ConfirmacionesEnMomentoDeNotificar)> CambiosRuta { get; } = new();

        public List<(Guid OperadorId, Guid HojaDeRutaId, Guid ParadaId, string EstadoParada, int ConfirmacionesEnMomentoDeNotificar)> HitosParada { get; } = new();

        public Task NotificarCambioRutaAsync(Guid operadorId, Guid hojaDeRutaId, string estado, CancellationToken ct = default)
        {
            if (LanzarExcepcion)
            {
                throw new InvalidOperationException("Error simulado en el Hub SignalR.");
            }

            CambiosRuta.Add((operadorId, hojaDeRutaId, estado, _uow.Confirmaciones));
            return Task.CompletedTask;
        }

        public Task NotificarHitoParadaAsync(Guid operadorId, Guid hojaDeRutaId, Guid paradaId, string estadoParada, CancellationToken ct = default)
        {
            if (LanzarExcepcion)
            {
                throw new InvalidOperationException("Error simulado en el Hub SignalR.");
            }

            HitosParada.Add((operadorId, hojaDeRutaId, paradaId, estadoParada, _uow.Confirmaciones));
            return Task.CompletedTask;
        }
    }
}
