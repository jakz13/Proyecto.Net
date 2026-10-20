using UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ConfirmarCarga;
using UltimaMilla.Application.Commands.Planificacion.ArmarHojaDeRuta;
using UltimaMilla.Application.Commands.Planificacion.DespacharHojaDeRuta;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

/// <summary>Cubre UC-18 (registrar la carga escaneada) y UC-09 (despacho por el operario) sobre una ruta armada.</summary>
public class DespachoYCargaCommandHandlerTests
{
    private static readonly DateOnly Viernes = new(2026, 10, 16);

    private readonly FakeRutaRepository _rutas = new();
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeOutboxStorage _outbox = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();
    private readonly Repartidor _repartidor;
    private readonly Vehiculo _vehiculo;
    private int _secuencia;

    public DespachoYCargaCommandHandlerTests()
    {
        _repartidor = new Repartidor(Guid.NewGuid(), Guid.NewGuid(), _tenant.OperadorId, "1.111.111-1", "099111111");
        _vehiculo = new Vehiculo(Guid.NewGuid(), _tenant.OperadorId, "SAB1234", "Furgoneta", 500m, 100000m);
    }

    private ConfirmarCargaCommandHandler HandlerCarga() => new(_rutas, _envios, _uow, _tenant);

    private DespacharHojaDeRutaCommandHandler HandlerDespacho() => new(_rutas, _envios, _outbox, _uow, _tenant, _clock);

    private Envio CrearEnvioClasificado(int bultos = 1)
    {
        _secuencia++;
        var codigo = $"TRK-DESP{_secuencia:0000}";
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", codigo,
            direccionEntregaId: Guid.NewGuid(), operadorId: _tenant.OperadorId);

        var codigos = new List<string>();
        for (var i = 1; i <= bultos; i++)
        {
            var id = $"{codigo}-B{i:00}";
            envio.AgregarBulto(id, 2m, 10m, 10m, 10m);
            codigos.Add(id);
        }

        envio.RecepcionarEnDeposito(codigos, null, null, Guid.NewGuid());
        envio.ClasificarBultos(codigos.Select(c => new ClasificacionBulto(c, PrioridadBulto.Normal)).ToList(), Guid.NewGuid());
        _envios.Envios.Add(envio);
        return envio;
    }

    /// <summary>Arma y libera una ruta con los envíos indicados directamente sobre el dominio.</summary>
    private HojaDeRuta CrearRutaLiberada(params Envio[] envios)
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, Viernes);
        ruta.AsignarRecursos(_repartidor.Id, _vehiculo.Id);
        var orden = 1;
        foreach (var envio in envios)
        {
            ruta.AgregarParada(envio.Id, orden++, envio.DireccionEntregaId!.Value);
            ruta.RegistrarAsignacion(envio.Id, Guid.NewGuid());
            envio.AsignarRuta(ruta.Id);
        }

        ruta.Liberar();
        _rutas.Rutas.Add(ruta);
        return ruta;
    }

    private static List<string> CodigosDe(params Envio[] envios) =>
        envios.SelectMany(e => e.Bultos).Select(b => b.Identificacion).ToList();

    private Task<ConfirmarCargaResultDto> Cargar(HojaDeRuta ruta, IEnumerable<string> codigos, string clave = "clave-1") =>
        HandlerCarga().Handle(new ConfirmarCargaCommand(ruta.Id, codigos.ToList(), clave), CancellationToken.None);

    private Task<DespachoResultDto> Despachar(HojaDeRuta ruta, string? observaciones = null) =>
        HandlerDespacho().Handle(new DespacharHojaDeRutaCommand(ruta.Id, observaciones), CancellationToken.None);

    // ---- UC-18: registrar la carga ----

    [Fact]
    public async Task Carga_CompletaSinDiscrepancias_DejaLaRutaEnCargaSinTocarLosEnvios()
    {
        var a = CrearEnvioClasificado();
        var b = CrearEnvioClasificado();
        var ruta = CrearRutaLiberada(a, b);

        var resultado = await Cargar(ruta, CodigosDe(a, b));

        Assert.Equal("EnCarga", resultado.EstadoRuta);
        Assert.Equal(2, resultado.TotalBultosCargados);
        Assert.Equal(0, resultado.TotalBultosFaltantes);
        Assert.False(resultado.Idempotente);
        Assert.Equal(2, ruta.EscaneosCarga.Count);
        Assert.All(new[] { a, b }, e => Assert.Equal(EstadoEnvio.AsignadoARuta, e.EstadoActual));
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task Carga_ConFaltantesYSobrantes_LosInformaYRegistraLoCargado()
    {
        var a = CrearEnvioClasificado(bultos: 2);
        var ruta = CrearRutaLiberada(a);

        var resultado = await Cargar(ruta, new[] { a.Bultos.First().Identificacion, "BULTO-AJENO" });

        Assert.Equal(1, resultado.TotalBultosCargados);
        Assert.Equal(1, resultado.TotalBultosFaltantes);
        Assert.Equal(1, resultado.TotalBultosSobrantes);
        Assert.Contains("discrepancias", resultado.Mensaje);
    }

    [Fact]
    public async Task Carga_ReenviadaConLaMismaInformacion_EsIdempotente()
    {
        var a = CrearEnvioClasificado();
        var ruta = CrearRutaLiberada(a);
        await Cargar(ruta, CodigosDe(a));

        var segunda = await Cargar(ruta, CodigosDe(a));

        Assert.True(segunda.Idempotente);
        Assert.Single(ruta.EscaneosCarga);
    }

    [Fact]
    public async Task Carga_NuevaVersionReemplazaALaAnterior()
    {
        var a = CrearEnvioClasificado(bultos: 2);
        var ruta = CrearRutaLiberada(a);
        await Cargar(ruta, new[] { a.Bultos.First().Identificacion });

        var resultado = await Cargar(ruta, CodigosDe(a));

        Assert.Equal(2, resultado.TotalBultosCargados);
        Assert.Equal(2, ruta.EscaneosCarga.Count);
    }

    [Fact]
    public async Task Carga_CodigosEnMinusculasYDuplicados_SeNormalizan()
    {
        var a = CrearEnvioClasificado();
        var ruta = CrearRutaLiberada(a);
        var codigo = a.Bultos.Single().Identificacion;

        var resultado = await Cargar(ruta, new[] { $"  {codigo.ToLowerInvariant()} ", codigo });

        Assert.Equal(1, resultado.TotalBultosCargados);
        Assert.Equal(0, resultado.TotalBultosSobrantes);
    }

    [Fact]
    public async Task Carga_SobreRutaYaDespachada_NoModificaNada()
    {
        var a = CrearEnvioClasificado();
        var ruta = CrearRutaLiberada(a);
        await Cargar(ruta, CodigosDe(a));
        await Despachar(ruta);
        var escaneos = ruta.EscaneosCarga.Count;

        var resultado = await Cargar(ruta, new[] { "OTRO" });

        Assert.True(resultado.Idempotente);
        Assert.Equal(escaneos, ruta.EscaneosCarga.Count);
        Assert.Equal("Despachada", ruta.Estado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Carga_SinClaveDeIdempotencia_LanzaDomainValidationException(string? clave)
    {
        var a = CrearEnvioClasificado();
        var ruta = CrearRutaLiberada(a);

        await Assert.ThrowsAsync<DomainValidationException>(() => Cargar(ruta, CodigosDe(a), clave!));
    }

    [Fact]
    public async Task Carga_SinNingunBulto_LanzaDomainValidationException()
    {
        var a = CrearEnvioClasificado();
        var ruta = CrearRutaLiberada(a);

        await Assert.ThrowsAsync<DomainValidationException>(() => Cargar(ruta, new List<string>()));
        await Assert.ThrowsAsync<DomainValidationException>(() => Cargar(ruta, new[] { "   " }));
        await Assert.ThrowsAsync<DomainValidationException>(() => Cargar(ruta, new[] { "NO-ES-DE-LA-RUTA" }));
        Assert.Equal("Liberada", ruta.Estado);
    }

    [Fact]
    public async Task Carga_RutaInexistenteOVacia_LanzaExcepcionesTipadas()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            HandlerCarga().Handle(new ConfirmarCargaCommand(Guid.NewGuid(), new List<string> { "X" }, "k"), CancellationToken.None));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            HandlerCarga().Handle(new ConfirmarCargaCommand(Guid.Empty, new List<string> { "X" }, "k"), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => HandlerCarga().Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Carga_RutaDeOtroOperador_LanzaUnauthorizedAccess()
    {
        var a = CrearEnvioClasificado();
        var ruta = CrearRutaLiberada(a);
        _tenant.OperadorId = Guid.NewGuid();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Cargar(ruta, CodigosDe(a)));
    }

    // ---- UC-09: despacho ----

    [Fact]
    public async Task Despacho_ConTodoCargado_PasaLosEnviosAEnTransitoYLaRutaADespachada()
    {
        var a = CrearEnvioClasificado();
        var b = CrearEnvioClasificado();
        var ruta = CrearRutaLiberada(a, b);
        await Cargar(ruta, CodigosDe(a, b));

        var resultado = await Despachar(ruta, "Salida normal");

        Assert.Equal("Despachada", resultado.Estado);
        Assert.NotNull(resultado.FechaDespacho);
        Assert.Equal(2, resultado.EnviosEnTransito);
        Assert.Empty(resultado.EnviosRetirados);
        Assert.False(resultado.Idempotente);
        Assert.All(new[] { a, b }, e =>
        {
            Assert.Equal(EstadoEnvio.EnTransito, e.EstadoActual);
            Assert.Equal("DespachoRuta", e.Eventos.Last().TipoEvento);
            Assert.Equal(_tenant.UsuarioId, e.Eventos.Last().ResponsableUsuarioId);
            Assert.All(e.Bultos, bu => Assert.Equal("CargadoEnVehiculo", bu.EstadoBulto));
        });
        Assert.Equal(3, _outbox.Mensajes.Count);
        Assert.Contains(_outbox.Mensajes, m => m.TipoEvento == "RutaDespachadaIntegrationEvent");
    }

    [Fact]
    public async Task Despacho_ConBultoNoCargado_RetiraElEnvioYVuelveADeposito()
    {
        var completo = CrearEnvioClasificado();
        var incompleto = CrearEnvioClasificado(bultos: 2);
        var ruta = CrearRutaLiberada(completo, incompleto);
        await Cargar(ruta, CodigosDe(completo).Append(incompleto.Bultos.First().Identificacion));

        var resultado = await Despachar(ruta);

        Assert.Equal(1, resultado.EnviosEnTransito);
        var retirado = Assert.Single(resultado.EnviosRetirados);
        Assert.Equal(incompleto.Id, retirado.EnvioId);
        Assert.Equal(EstadoEnvio.EnTransito, completo.EstadoActual);
        Assert.Equal(EstadoEnvio.EnDeposito, incompleto.EstadoActual);
        Assert.Null(incompleto.HojaDeRutaId);
        Assert.False(ruta.Asignaciones.Single(x => x.EnvioId == incompleto.Id).Vigente);
        Assert.Equal("Cancelada", ruta.Paradas.Single(p => p.EnvioId == incompleto.Id).Estado);
        Assert.Contains(_outbox.Mensajes, m => m.TipoEvento == "EnvioDesasignadoPorFaltanteIntegrationEvent");
    }

    [Fact]
    public async Task Despacho_EnvioRetirado_QuedaDisponibleParaOtraRuta()
    {
        var a = CrearEnvioClasificado(bultos: 2);
        var b = CrearEnvioClasificado();
        var ruta = CrearRutaLiberada(a, b);
        await Cargar(ruta, CodigosDe(b));
        await Despachar(ruta);

        Assert.Null(a.ObtenerMotivoNoAsignable());
        Assert.True(a.EstaClasificado);
    }

    [Fact]
    public async Task Despacho_NingunEnvioCompleto_LanzaDomainValidationExceptionSinCambios()
    {
        var a = CrearEnvioClasificado(bultos: 2);
        var ruta = CrearRutaLiberada(a);
        await Cargar(ruta, new[] { a.Bultos.First().Identificacion });
        var confirmaciones = _uow.Confirmaciones;

        await Assert.ThrowsAsync<DomainValidationException>(() => Despachar(ruta));

        Assert.Equal("EnCarga", ruta.Estado);
        Assert.Equal(EstadoEnvio.AsignadoARuta, a.EstadoActual);
        Assert.Equal(confirmaciones, _uow.Confirmaciones);
        Assert.Empty(_outbox.Mensajes);
    }

    [Fact]
    public async Task Despacho_SinCargaSincronizada_LanzaDomainValidationException()
    {
        var a = CrearEnvioClasificado();
        var ruta = CrearRutaLiberada(a);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => Despachar(ruta));

        Assert.Contains("escaneada y sincronizada", ex.Message);
        Assert.Equal(EstadoEnvio.AsignadoARuta, a.EstadoActual);
    }

    [Fact]
    public async Task Despacho_Repetido_EsIdempotenteYNoDuplicaEventos()
    {
        var a = CrearEnvioClasificado();
        var ruta = CrearRutaLiberada(a);
        await Cargar(ruta, CodigosDe(a));
        await Despachar(ruta);
        var eventos = a.Eventos.Count;
        var mensajes = _outbox.Mensajes.Count;

        var segundo = await Despachar(ruta);

        Assert.True(segundo.Idempotente);
        Assert.Equal(1, segundo.EnviosEnTransito);
        Assert.Equal(eventos, a.Eventos.Count);
        Assert.Equal(mensajes, _outbox.Mensajes.Count);
    }

    [Fact]
    public async Task Despacho_ConOtraRutaDelRepartidorSinRendir_LanzaDomainValidationException()
    {
        var a = CrearEnvioClasificado();
        var b = CrearEnvioClasificado();
        var primera = CrearRutaLiberada(a);
        var segunda = CrearRutaLiberada(b);
        await Cargar(primera, CodigosDe(a));
        await Cargar(segunda, CodigosDe(b), "clave-2");
        await Despachar(primera);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => Despachar(segunda));

        Assert.Contains("sin rendir", ex.Message);
        Assert.Equal(EstadoEnvio.AsignadoARuta, b.EstadoActual);
    }

    [Fact]
    public async Task Despacho_RutaInexistenteOIdVacio_LanzaExcepcionesTipadas()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            HandlerDespacho().Handle(new DespacharHojaDeRutaCommand(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            HandlerDespacho().Handle(new DespacharHojaDeRutaCommand(Guid.Empty), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => HandlerDespacho().Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Despacho_RutaDeOtroOperador_LanzaUnauthorizedAccess()
    {
        var a = CrearEnvioClasificado();
        var ruta = CrearRutaLiberada(a);
        await Cargar(ruta, CodigosDe(a));
        _tenant.OperadorId = Guid.NewGuid();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Despachar(ruta));
        Assert.Equal("EnCarga", ruta.Estado);
    }

    [Fact]
    public async Task Despacho_UsuarioNoIdentificado_LanzaUnauthorizedAccess()
    {
        var a = CrearEnvioClasificado();
        var ruta = CrearRutaLiberada(a);
        await Cargar(ruta, CodigosDe(a));
        _tenant.UsuarioId = null;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Despachar(ruta));
    }
}
