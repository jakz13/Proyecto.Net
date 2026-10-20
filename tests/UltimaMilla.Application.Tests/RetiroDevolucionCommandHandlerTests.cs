using UltimaMilla.Application.Commands.Mobile.DevolucionEnCalle.RegistrarRetiroDevolucion;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class RetiroDevolucionCommandHandlerTests
{
    private readonly FakeRutaRepository _rutas = new();
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeDevolucionRepository _devoluciones = new();
    private readonly FakeOutboxStorage _outbox = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();

    private RegistrarRetiroDevolucionCommandHandler CrearHandler() =>
        new(_rutas, _envios, _devoluciones, _outbox, _uow, _tenant, _clock);

    /// <summary>Envío despachado cuyo único intento fallido alcanza el máximo configurado y pasa a devolución.</summary>
    private (HojaDeRuta Ruta, Envio Envio, Guid ParadaId) CrearEnvioEnDevolucion(bool enDevolucion = true, int bultos = 2)
    {
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-DEVO0001", Guid.NewGuid(),
            null, null, "Rivera 10", "Ana", _tenant.OperadorId);
        var codigos = new List<string>();
        for (var i = 1; i <= bultos; i++)
        {
            envio.AgregarBulto($"TRK-DEVO0001-B{i:00}", 2m, 10m, 10m, 10m);
            codigos.Add($"TRK-DEVO0001-B{i:00}");
        }

        envio.RecepcionarEnDeposito(codigos, null, null, Guid.NewGuid());
        envio.ClasificarBultos(codigos.Select(c => new ClasificacionBulto(c, PrioridadBulto.Normal)).ToList(), Guid.NewGuid());

        var ruta = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, new DateOnly(2026, 10, 16));
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        ruta.AgregarParada(envio.Id, 1, envio.DireccionEntregaId!.Value);
        ruta.RegistrarAsignacion(envio.Id, Guid.NewGuid());
        envio.AsignarRuta(ruta.Id);
        ruta.Liberar();
        ruta.RegistrarCargaEscaneada(envio.Bultos.Select(b => b.Id));
        ruta.Despachar();
        envio.DespacharEnRuta(ruta.Id);

        var paradaId = ruta.Paradas.Single().Id;
        if (enDevolucion)
        {
            var regla = new ReglaOperativa(Guid.NewGuid(), _tenant.OperadorId, 1, DateTime.UtcNow.AddDays(-1), 1,
                TimeSpan.FromHours(1), "Firma", "Dev", TimeSpan.FromHours(48), TimeSpan.FromHours(24));
            var motivo = new MotivoNoEntrega(Guid.NewGuid(), _tenant.OperadorId, "DIR", "Dirección incorrecta");
            envio.RegistrarIntentoFallido(paradaId, motivo, regla, "No existe la calle", null, null, null, DateTime.UtcNow);
            ruta.RegistrarIntentoFallidoEnParada(paradaId);
        }

        _rutas.Rutas.Add(ruta);
        _envios.Envios.Add(envio);
        return (ruta, envio, paradaId);
    }

    private Task<RetiroDevolucionResultDto> Retirar(HojaDeRuta ruta, Envio envio, Guid paradaId, string clave = "k1") =>
        CrearHandler().Handle(new RegistrarRetiroDevolucionCommand(ruta.Id, paradaId, envio.Id, clave, -34.9, -56.1, DateTime.UtcNow), default);

    [Fact]
    public async Task Retiro_MarcaLosBultosEnRetornoYRegistraLaDevolucion()
    {
        var (ruta, envio, paradaId) = CrearEnvioEnDevolucion();

        var resultado = await Retirar(ruta, envio, paradaId);

        Assert.Equal("EnDevolucion", resultado.EstadoEnvio);
        Assert.Equal("EnCaminoADeposito", resultado.EstadoDevolucion);
        Assert.Equal(2, resultado.BultosEnRetorno);
        Assert.False(resultado.Idempotente);
        Assert.All(envio.Bultos, b => Assert.Equal("EnRetornoADeposito", b.EstadoBulto));
        Assert.Equal(EstadoEnvio.EnDevolucion, envio.EstadoActual);
        var evento = envio.Eventos.Last();
        Assert.Equal("RetiroParaDevolucion", evento.TipoEvento);
        Assert.Equal(-34.9, evento.Latitud);
        Assert.Single(_devoluciones.Devoluciones);
        Assert.Single(_outbox.Mensajes, m => m.TipoEvento == "DevolucionEnCaminoIntegrationEvent");
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task Retiro_Repetido_EsIdempotenteYNoDuplicaEventos()
    {
        var (ruta, envio, paradaId) = CrearEnvioEnDevolucion();
        await Retirar(ruta, envio, paradaId);
        var eventos = envio.Eventos.Count;

        var segundo = await Retirar(ruta, envio, paradaId, "k2");

        Assert.True(segundo.Idempotente);
        Assert.Equal(eventos, envio.Eventos.Count);
        Assert.Single(_devoluciones.Devoluciones);
        Assert.Equal("EnCaminoADeposito", segundo.EstadoDevolucion);
    }

    [Fact]
    public async Task Retiro_EnvioQueNoEstaEnDevolucion_LanzaDomainValidationException()
    {
        var (ruta, envio, paradaId) = CrearEnvioEnDevolucion(enDevolucion: false);

        await Assert.ThrowsAsync<DomainValidationException>(() => Retirar(ruta, envio, paradaId));

        Assert.All(envio.Bultos, b => Assert.Equal("CargadoEnVehiculo", b.EstadoBulto));
        Assert.Empty(_devoluciones.Devoluciones);
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task Retiro_ParadaDeOtroEnvio_LanzaDomainValidationException()
    {
        var (ruta, envio, _) = CrearEnvioEnDevolucion();

        await Assert.ThrowsAsync<NotFoundException>(() => Retirar(ruta, envio, Guid.NewGuid()));
    }

    [Fact]
    public async Task Retiro_EnvioDistintoAlDeLaParada_LanzaDomainValidationException()
    {
        var (ruta, _, paradaId) = CrearEnvioEnDevolucion();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(new RegistrarRetiroDevolucionCommand(ruta.Id, paradaId, Guid.NewGuid(), "k"), default));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Retiro_SinClaveDeIdempotencia_LanzaDomainValidationException(string? clave)
    {
        var (ruta, envio, paradaId) = CrearEnvioEnDevolucion();

        await Assert.ThrowsAsync<DomainValidationException>(() => Retirar(ruta, envio, paradaId, clave!));
    }

    [Fact]
    public async Task Retiro_IdentificadoresVaciosONulos_LanzanExcepcionesTipadas()
    {
        var (ruta, envio, paradaId) = CrearEnvioEnDevolucion();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(new RegistrarRetiroDevolucionCommand(Guid.Empty, paradaId, envio.Id, "k"), default));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(new RegistrarRetiroDevolucionCommand(ruta.Id, Guid.Empty, envio.Id, "k"), default));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(new RegistrarRetiroDevolucionCommand(ruta.Id, paradaId, Guid.Empty, "k"), default));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CrearHandler().Handle(new RegistrarRetiroDevolucionCommand(Guid.NewGuid(), paradaId, envio.Id, "k"), default));
        await Assert.ThrowsAsync<ArgumentNullException>(() => CrearHandler().Handle(null!, default));
    }

    [Fact]
    public async Task Retiro_RutaDeOtroOperador_LanzaUnauthorizedAccess()
    {
        var (ruta, envio, paradaId) = CrearEnvioEnDevolucion();
        _tenant.OperadorId = Guid.NewGuid();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Retirar(ruta, envio, paradaId));
        Assert.All(envio.Bultos, b => Assert.NotEqual("EnRetornoADeposito", b.EstadoBulto));
    }

    [Fact]
    public async Task Rendicion_ReconoceLosBultosEnRetornoComoParteDelViaje()
    {
        var (ruta, envio, paradaId) = CrearEnvioEnDevolucion();
        await Retirar(ruta, envio, paradaId);

        var handler = new UltimaMilla.Application.Commands.Deposito.RendirHojaDeRuta.RendirHojaDeRutaCommandHandler(
            _rutas, _envios, new FakeRendicionRepository(), _outbox, _uow, _tenant, _clock);
        var resultado = await handler.Handle(
            new UltimaMilla.Application.Commands.Deposito.RendirHojaDeRuta.RendirHojaDeRutaCommand(
                ruta.Id, envio.Bultos.Select(b => b.Identificacion).ToList()), default);

        Assert.Equal(2, resultado.BultosDevueltos);
        Assert.Equal(0, resultado.BultosPendientes);
        Assert.Contains(envio.CodigoSeguimientoPublico, resultado.EnviosEnDevolucion);
        Assert.Equal(EstadoEnvio.EnDevolucion, envio.EstadoActual);
    }
}
