using UltimaMilla.Application.Commands.Seguimiento.SolicitarReprogramacion;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Seguimiento.ListarFranjasDisponibles;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Tests;

public class ReprogramacionDestinatarioHandlerTests
{
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeDireccionRepository _direcciones = new();
    private readonly FakeFranjaHorariaRepository _franjas = new();
    private readonly FakeSolicitudReprogramacionRepository _solicitudes = new();
    private readonly FakeOutboxStorage _outbox = new();
    private readonly FakeClock _clock = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly Guid _operadorId = Guid.NewGuid();
    private readonly Guid _zonaId = Guid.NewGuid();
    private readonly FranjaHoraria _manana;
    private readonly FranjaHoraria _tarde;
    private readonly Envio _envio;

    public ReprogramacionDestinatarioHandlerTests()
    {
        _manana = new FranjaHoraria(Guid.NewGuid(), _zonaId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0));
        _tarde = new FranjaHoraria(Guid.NewGuid(), _zonaId, DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0));
        _franjas.Franjas.AddRange(new[] { _manana, _tarde });

        var direccion = new Direccion(Guid.NewGuid(), "18 de Julio", "1234", "Montevideo", _zonaId);
        _direcciones.Direcciones.Add(direccion);

        _envio = new Envio(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-REPROG01", direccion.Id, _manana.Id, operadorId: _operadorId);
        _envio.TransicionarEstado(EstadoEnvio.EnDeposito, "RecepcionEnDeposito");
        _envios.Envios.Add(_envio);

        _envios.ReglaOperativa = new ReglaOperativa(
            Guid.NewGuid(), _operadorId, 1, DateTime.UtcNow.AddDays(-10), 3, TimeSpan.FromHours(24),
            "Firma digital", "Devolución automática a depósito", TimeSpan.FromHours(48), TimeSpan.FromHours(24));
    }

    private SolicitarReprogramacionCommandHandler Solicitar() =>
        new(_envios, _direcciones, _franjas, _solicitudes, _outbox, _clock, _uow);

    private ListarFranjasDisponiblesQueryHandler Listar() => new(_envios, _direcciones, _franjas);

    [Fact]
    public async Task Solicitar_FranjaDeLaZona_SeAceptaYActualizaElEnvio()
    {
        var resultado = await Solicitar().Handle(new SolicitarReprogramacionCommand("trk-reprog01", _tarde.Id, "10.0.0.1"), default);

        Assert.True(resultado.Aceptada);
        Assert.Equal("Aprobada", resultado.Estado);
        Assert.Equal("Monday 14:00-18:00", resultado.FranjaAsignada);
        Assert.Equal(_tarde.Id, _envio.FranjaHorariaComprometidaId);
        Assert.Equal(EstadoEnvio.EnDeposito, _envio.EstadoActual);
        Assert.Equal("ReprogramacionSolicitadaPorDestinatario", _envio.Eventos.Last().TipoEvento);
        var solicitud = Assert.Single(_solicitudes.Solicitudes);
        Assert.Equal("10.0.0.1", solicitud.OrigenIp);
        Assert.Single(_outbox.Mensajes);
    }

    [Fact]
    public async Task Solicitar_FranjaDeOtraZona_SeRechazaConMotivoYNoCambiaElEnvio()
    {
        var ajena = new FranjaHoraria(Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(12, 0));
        _franjas.Franjas.Add(ajena);

        var resultado = await Solicitar().Handle(new SolicitarReprogramacionCommand(_envio.CodigoSeguimientoPublico, ajena.Id), default);

        Assert.False(resultado.Aceptada);
        Assert.Equal("Rechazada", resultado.Estado);
        Assert.Contains("no está disponible", resultado.Motivo);
        Assert.Equal(_manana.Id, _envio.FranjaHorariaComprometidaId);
        Assert.Equal("Rechazada", Assert.Single(_solicitudes.Solicitudes).Estado);
        Assert.Empty(_outbox.Mensajes);
    }

    [Fact]
    public async Task Solicitar_FranjaInactiva_SeRechaza()
    {
        _tarde.Desactivar();

        var resultado = await Solicitar().Handle(new SolicitarReprogramacionCommand(_envio.CodigoSeguimientoPublico, _tarde.Id), default);

        Assert.False(resultado.Aceptada);
    }

    [Fact]
    public async Task Solicitar_MismaFranjaQueYaTiene_SeRechaza()
    {
        var resultado = await Solicitar().Handle(new SolicitarReprogramacionCommand(_envio.CodigoSeguimientoPublico, _manana.Id), default);

        Assert.False(resultado.Aceptada);
        Assert.Contains("ya tiene asignada", resultado.Motivo);
    }

    [Theory]
    [InlineData(EstadoEnvio.AsignadoARuta)]
    [InlineData(EstadoEnvio.EnTransito)]
    public async Task Solicitar_EnvioEnPreparacionParaReparto_SeRechazaSinTocarlo(EstadoEnvio estado)
    {
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-RUTA0001", _envio.DireccionEntregaId, _manana.Id, operadorId: _operadorId);
        envio.TransicionarEstado(EstadoEnvio.EnDeposito, "RecepcionEnDeposito");
        envio.TransicionarEstado(EstadoEnvio.AsignadoARuta, "AsignacionARuta");
        if (estado == EstadoEnvio.EnTransito)
            envio.TransicionarEstado(EstadoEnvio.EnTransito, "Despacho");
        _envios.Envios.Add(envio);

        var resultado = await Solicitar().Handle(new SolicitarReprogramacionCommand("TRK-RUTA0001", _tarde.Id), default);

        Assert.False(resultado.Aceptada);
        Assert.Equal(_manana.Id, envio.FranjaHorariaComprometidaId);
    }

    [Fact]
    public async Task Solicitar_EnvioEntregado_SeRechazaPorNoEstarPendiente()
    {
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-ENTREG001", _envio.DireccionEntregaId, operadorId: _operadorId);
        envio.TransicionarEstado(EstadoEnvio.EnDeposito, "RecepcionEnDeposito");
        envio.TransicionarEstado(EstadoEnvio.AsignadoARuta, "AsignacionARuta");
        envio.TransicionarEstado(EstadoEnvio.EnTransito, "Despacho");
        envio.TransicionarEstado(EstadoEnvio.Entregado, "EntregaExitosa");
        _envios.Envios.Add(envio);

        var resultado = await Solicitar().Handle(new SolicitarReprogramacionCommand("TRK-ENTREG001", _tarde.Id), default);

        Assert.False(resultado.Aceptada);
        Assert.Contains("pendiente de entrega", resultado.Motivo);
    }

    [Fact]
    public async Task Solicitar_AlcanzadoElMaximoDeReprogramaciones_SeRechaza()
    {
        // Con 3 intentos máximos, dos reprogramaciones aprobadas más un intento ya realizado agotan el cupo
        _solicitudes.Aprobadas = 2;
        _envio.TransicionarEstado(EstadoEnvio.AsignadoARuta, "AsignacionARuta");
        _envio.TransicionarEstado(EstadoEnvio.EnTransito, "Despacho");
        _envio.RegistrarIntentoFallido(
            Guid.NewGuid(),
            new MotivoNoEntrega(Guid.NewGuid(), _operadorId, "AUSENTE", "Ausente"),
            _envios.ReglaOperativa!, "Nadie atendió", null, null, null, DateTime.UtcNow);

        var resultado = await Solicitar().Handle(new SolicitarReprogramacionCommand(_envio.CodigoSeguimientoPublico, _tarde.Id), default);

        Assert.False(resultado.Aceptada);
        Assert.Contains("máximo de reprogramaciones", resultado.Motivo);
    }

    [Theory]
    [InlineData("TRK-NOEXISTE")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Solicitar_CodigoInexistenteOInvalido_LanzaNotFound(string? codigo)
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Solicitar().Handle(new SolicitarReprogramacionCommand(codigo, _tarde.Id), default));

        Assert.Empty(_solicitudes.Solicitudes);
    }

    [Fact]
    public async Task ListarFranjas_EnvioPendiente_DevuelveLasFranjasActivasDeLaZonaMarcandoLaActual()
    {
        _franjas.Franjas.Add(new FranjaHoraria(Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0)));

        var resultado = await Listar().Handle(new ListarFranjasDisponiblesQuery(_envio.CodigoSeguimientoPublico), default);

        Assert.NotNull(resultado);
        Assert.True(resultado!.PuedeReprogramar);
        Assert.Equal(2, resultado.Franjas.Count);
        Assert.Equal("09:00", resultado.Franjas[0].HoraInicio);
        Assert.True(resultado.Franjas.Single(f => f.Id == _manana.Id).EsLaActual);
    }

    [Fact]
    public async Task ListarFranjas_EnvioEntregado_NoPermiteReprogramar()
    {
        _envio.TransicionarEstado(EstadoEnvio.AsignadoARuta, "AsignacionARuta");
        _envio.TransicionarEstado(EstadoEnvio.EnTransito, "Despacho");
        _envio.TransicionarEstado(EstadoEnvio.Entregado, "EntregaExitosa");

        var resultado = await Listar().Handle(new ListarFranjasDisponiblesQuery(_envio.CodigoSeguimientoPublico), default);

        Assert.False(resultado!.PuedeReprogramar);
        Assert.Empty(resultado.Franjas);
    }

    [Fact]
    public async Task ListarFranjas_CodigoInexistente_DevuelveNull()
    {
        Assert.Null(await Listar().Handle(new ListarFranjasDisponiblesQuery("TRK-NOEXISTE"), default));
    }
}

public class FakeSolicitudReprogramacionRepository : UltimaMilla.Application.Ports.ISolicitudReprogramacionRepository
{
    public List<SolicitudReprogramacion> Solicitudes { get; } = new();

    /// <summary>Cantidad base de aprobadas previas, además de las agregadas en la prueba.</summary>
    public int Aprobadas { get; set; }

    public Task AddAsync(SolicitudReprogramacion solicitud, CancellationToken cancellationToken = default)
    {
        Solicitudes.Add(solicitud);
        return Task.CompletedTask;
    }

    public Task<int> ContarAprobadasAsync(Guid envioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Aprobadas + Solicitudes.Count(s => s.EnvioId == envioId && s.Estado == "Aprobada"));
}
