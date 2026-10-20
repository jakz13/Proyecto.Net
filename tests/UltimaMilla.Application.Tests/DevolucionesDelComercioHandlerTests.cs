using UltimaMilla.Application.Commands.Comercio.ConfirmarDevolucion;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Comercio.ListarDevolucionesDelComercio;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class DevolucionesDelComercioHandlerTests
{
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeDevolucionRepository _devoluciones = new();
    private readonly FakeOutboxStorage _outbox = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();
    private int _secuencia;

    private ConfirmarDevolucionCommandHandler HandlerConfirmar() => new(_envios, _devoluciones, _outbox, _uow, _tenant, _clock);

    private ListarDevolucionesDelComercioQueryHandler HandlerListar() => new(_envios, _devoluciones, _tenant);

    private Envio CrearEnvioEnDevolucion(Guid? comercio = null, Guid? operador = null, bool enDevolucion = true)
    {
        _secuencia++;
        var codigo = $"TRK-DCOM{_secuencia:0000}";
        var envio = new Envio(Guid.NewGuid(), comercio ?? _tenant.ComercioOperadorId!.Value, Guid.NewGuid(), "Estandar", codigo,
            Guid.NewGuid(), null, null, "Rivera 10", "Ana", operador ?? _tenant.OperadorId);
        envio.AgregarBulto($"{codigo}-B01", 1m, 10m, 10m, 10m);
        envio.RecepcionarEnDeposito(new[] { $"{codigo}-B01" }, null, null, Guid.NewGuid());
        envio.AsignarRuta(Guid.NewGuid());
        envio.DespacharEnRuta(envio.HojaDeRutaId!.Value);

        if (enDevolucion)
        {
            var regla = new ReglaOperativa(Guid.NewGuid(), _tenant.OperadorId, 1, DateTime.UtcNow.AddDays(-1), 1,
                TimeSpan.FromHours(1), "Firma", "Dev", TimeSpan.FromHours(48), TimeSpan.FromHours(24));
            var motivo = new MotivoNoEntrega(Guid.NewGuid(), _tenant.OperadorId, "DIR", "Dirección incorrecta");
            envio.RegistrarIntentoFallido(Guid.NewGuid(), motivo, regla, "No existe", null, null, null, DateTime.UtcNow);
        }

        _envios.Envios.Add(envio);
        return envio;
    }

    // ---- Consulta de devoluciones ----

    [Fact]
    public async Task Listar_DevuelveLasDevolucionesAbiertasDelComercioConSuEstado()
    {
        var conRegistro = CrearEnvioEnDevolucion();
        var sinRegistro = CrearEnvioEnDevolucion();
        CrearEnvioEnDevolucion(enDevolucion: false);
        CrearEnvioEnDevolucion(comercio: Guid.NewGuid());
        var devolucion = new Devolucion(Guid.NewGuid(), conRegistro.Id);
        devolucion.MarcarEnCaminoADeposito();
        _devoluciones.Devoluciones.Add(devolucion);

        var pagina = await HandlerListar().Handle(new ListarDevolucionesDelComercioQuery(), default);

        Assert.Equal(2, pagina.Total);
        var a = pagina.Items.Single(i => i.EnvioId == conRegistro.Id);
        var b = pagina.Items.Single(i => i.EnvioId == sinRegistro.Id);
        Assert.Equal("EnCaminoADeposito", a.EstadoDevolucion);
        Assert.Equal("EnDevolucion", b.EstadoDevolucion);
        Assert.All(pagina.Items, i => Assert.True(i.PuedeConfirmarRecepcion));
    }

    [Fact]
    public async Task Listar_SoloCerradas_DevuelveLasYaConfirmadas()
    {
        var envio = CrearEnvioEnDevolucion();
        await HandlerConfirmar().Handle(new ConfirmarDevolucionCommand(envio.Id), default);
        CrearEnvioEnDevolucion();

        var cerradas = await HandlerListar().Handle(new ListarDevolucionesDelComercioQuery(SoloCerradas: true), default);

        var item = Assert.Single(cerradas.Items);
        Assert.Equal(envio.Id, item.EnvioId);
        Assert.Equal("Devuelto", item.EstadoDevolucion);
        Assert.False(item.PuedeConfirmarRecepcion);
        Assert.NotNull(item.FechaCierre);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Listar_PaginacionInvalida_LanzaDomainValidationException(int pagina, int tamano)
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            HandlerListar().Handle(new ListarDevolucionesDelComercioQuery(false, pagina, tamano), default));
    }

    [Fact]
    public async Task Listar_UsuarioSinComercioOConsultaNula_LanzaExcepcionesTipadas()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => HandlerListar().Handle(null!, default));
        _tenant.ComercioOperadorId = null;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => HandlerListar().Handle(new ListarDevolucionesDelComercioQuery(), default));
    }

    // ---- Confirmación de recepción ----

    [Fact]
    public async Task Confirmar_CierraLaDevolucionYPasaElEnvioADevuelto()
    {
        var envio = CrearEnvioEnDevolucion();

        var resultado = await HandlerConfirmar().Handle(new ConfirmarDevolucionCommand(envio.Id), default);

        Assert.Equal("Devuelto", resultado.EstadoEnvio);
        Assert.Equal("Devuelto", resultado.EstadoDevolucion);
        Assert.False(resultado.Idempotente);
        Assert.Equal(EstadoEnvio.Devuelto, envio.EstadoActual);
        Assert.Equal("DevolucionConfirmadaPorComercio", envio.Eventos.Last().TipoEvento);
        Assert.Equal("PortalComercio", envio.Eventos.Last().Origen);
        var devolucion = Assert.Single(_devoluciones.Devoluciones);
        Assert.Equal("Devuelto", devolucion.Estado);
        Assert.NotNull(devolucion.FechaRendicion);
        Assert.Single(_outbox.Mensajes, m => m.TipoEvento == "EnvioDevueltoIntegrationEvent");
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task Confirmar_ConDevolucionEnCamino_LaCierra()
    {
        var envio = CrearEnvioEnDevolucion();
        var devolucion = new Devolucion(Guid.NewGuid(), envio.Id);
        devolucion.MarcarEnCaminoADeposito();
        _devoluciones.Devoluciones.Add(devolucion);

        await HandlerConfirmar().Handle(new ConfirmarDevolucionCommand(envio.Id), default);

        Assert.Single(_devoluciones.Devoluciones);
        Assert.Equal("Devuelto", devolucion.Estado);
    }

    [Fact]
    public async Task Confirmar_Repetido_EsIdempotente()
    {
        var envio = CrearEnvioEnDevolucion();
        await HandlerConfirmar().Handle(new ConfirmarDevolucionCommand(envio.Id), default);
        var eventos = envio.Eventos.Count;

        var segundo = await HandlerConfirmar().Handle(new ConfirmarDevolucionCommand(envio.Id), default);

        Assert.True(segundo.Idempotente);
        Assert.Equal(eventos, envio.Eventos.Count);
        Assert.Equal(1, _uow.Confirmaciones);
        Assert.Single(_outbox.Mensajes);
    }

    [Fact]
    public async Task Confirmar_EnvioQueNoEstaEnDevolucion_LanzaDomainValidationException()
    {
        var envio = CrearEnvioEnDevolucion(enDevolucion: false);

        await Assert.ThrowsAsync<DomainValidationException>(() => HandlerConfirmar().Handle(new ConfirmarDevolucionCommand(envio.Id), default));

        Assert.Equal(EstadoEnvio.EnTransito, envio.EstadoActual);
        Assert.Empty(_devoluciones.Devoluciones);
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task Confirmar_EnvioDeOtroComercioOOperador_LanzaUnauthorizedAccessSinCambios()
    {
        var ajenoComercio = CrearEnvioEnDevolucion(comercio: Guid.NewGuid());
        var ajenoOperador = CrearEnvioEnDevolucion(operador: Guid.NewGuid());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => HandlerConfirmar().Handle(new ConfirmarDevolucionCommand(ajenoComercio.Id), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => HandlerConfirmar().Handle(new ConfirmarDevolucionCommand(ajenoOperador.Id), default));

        Assert.Equal(EstadoEnvio.EnDevolucion, ajenoComercio.EstadoActual);
        Assert.Equal(EstadoEnvio.EnDevolucion, ajenoOperador.EstadoActual);
    }

    [Fact]
    public async Task Confirmar_UsuarioSinComercio_LanzaUnauthorizedAccess()
    {
        var envio = CrearEnvioEnDevolucion();
        _tenant.ComercioOperadorId = null;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => HandlerConfirmar().Handle(new ConfirmarDevolucionCommand(envio.Id), default));
    }

    [Fact]
    public async Task Confirmar_InexistenteIdVacioOComandoNulo_LanzaExcepcionesTipadas()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => HandlerConfirmar().Handle(new ConfirmarDevolucionCommand(Guid.NewGuid()), default));
        await Assert.ThrowsAsync<DomainValidationException>(() => HandlerConfirmar().Handle(new ConfirmarDevolucionCommand(Guid.Empty), default));
        await Assert.ThrowsAsync<ArgumentNullException>(() => HandlerConfirmar().Handle(null!, default));
    }
}
