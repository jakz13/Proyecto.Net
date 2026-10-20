using UltimaMilla.Application.Commands.Planificacion.ArmarHojaDeRuta;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Planificacion;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class ConsultasPlanificacionQueryHandlerTests
{
    private static readonly DateOnly Viernes = new(2026, 10, 16);

    private readonly FakeRutaRepository _rutas = new();
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeRepartidorRepository _repartidores = new();
    private readonly FakeVehiculoRepository _vehiculos = new();
    private readonly FakeFranjaHorariaRepository _franjas = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly Repartidor _repartidor;
    private readonly Vehiculo _vehiculo;
    private int _secuencia;

    public ConsultasPlanificacionQueryHandlerTests()
    {
        _repartidor = new Repartidor(Guid.NewGuid(), Guid.NewGuid(), _tenant.OperadorId, "1.111.111-1", "099111111");
        _vehiculo = new Vehiculo(Guid.NewGuid(), _tenant.OperadorId, "SAB1234", "Furgoneta", 500m, 10m);
        _repartidores.Repartidores.Add(_repartidor);
        _vehiculos.Vehiculos.Add(_vehiculo);
    }

    private Envio CrearEnvio(bool clasificar = true, bool recepcionar = true, Guid? operadorId = null, int bultos = 1, Guid? franja = null)
    {
        _secuencia++;
        var codigo = $"TRK-PLAN{_secuencia:0000}";
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", codigo, Guid.NewGuid(), franja,
            null, $"Calle {_secuencia}, Montevideo", $"Destinatario {_secuencia}", operadorId ?? _tenant.OperadorId);

        var codigos = new List<string>();
        for (var i = 1; i <= bultos; i++)
        {
            envio.AgregarBulto($"{codigo}-B{i:00}", 2m, 10m, 10m, 10m);
            codigos.Add($"{codigo}-B{i:00}");
        }

        if (recepcionar)
            envio.RecepcionarEnDeposito(codigos, null, null, Guid.NewGuid());
        if (recepcionar && clasificar)
            envio.ClasificarBultos(codigos.Select(c => new ClasificacionBulto(c, PrioridadBulto.Alta)).ToList(), Guid.NewGuid());

        _envios.Envios.Add(envio);
        return envio;
    }

    private HojaDeRuta CrearRuta(Envio[] envios, DateOnly? fecha = null, Guid? operadorId = null, Guid? repartidorId = null, string estado = "Liberada")
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), operadorId ?? _tenant.OperadorId, fecha ?? Viernes);
        ruta.AsignarRecursos(repartidorId ?? _repartidor.Id, _vehiculo.Id);
        var orden = 1;
        foreach (var envio in envios)
        {
            ruta.AgregarParada(envio.Id, orden++, envio.DireccionEntregaId!.Value, envio.FranjaHorariaComprometidaId);
            ruta.RegistrarAsignacion(envio.Id, Guid.NewGuid());
            envio.AsignarRuta(ruta.Id);
        }

        ruta.Liberar();
        if (estado is "EnCarga" or "Despachada")
            ruta.RegistrarCargaEscaneada(envios.SelectMany(e => e.Bultos).Select(b => b.Id));
        if (estado == "Despachada")
            ruta.Despachar();

        _rutas.Rutas.Add(ruta);
        return ruta;
    }

    private ListarHojasDeRutaQueryHandler HandlerListar() => new(_rutas, _envios, _vehiculos, _repartidores, _tenant);

    private ObtenerHojaDeRutaQueryHandler HandlerObtener() => new(_rutas, _envios, _vehiculos, _repartidores, _franjas, _tenant);

    private ObtenerVerificacionDespachoQueryHandler HandlerVerificacion() => new(_rutas, _envios, _vehiculos, _tenant);

    // ---- Listado ----

    [Fact]
    public async Task Listar_DevuelveResumenConTotalesVehiculoYRepartidor()
    {
        var a = CrearEnvio();
        var b = CrearEnvio();
        var ruta = CrearRuta(new[] { a, b });

        var lista = await HandlerListar().Handle(new ListarHojasDeRutaQuery(), default);

        var resumen = Assert.Single(lista);
        Assert.Equal(ruta.Id, resumen.HojaDeRutaId);
        Assert.StartsWith("HR-20261016-", resumen.NumeroRuta);
        Assert.Equal("Liberada", resumen.Estado);
        Assert.Equal(2, resumen.CantidadParadas);
        Assert.Equal(4m, resumen.PesoTotalKg);
        Assert.Equal(0.002m, resumen.VolumenTotalM3);
        Assert.Equal("SAB1234", resumen.VehiculoPatente);
        Assert.Equal(500m, resumen.CapacidadPesoKg);
        Assert.Equal("1.111.111-1", resumen.RepartidorDocumento);
        Assert.Equal(ruta.SelloModificacion, resumen.SelloModificacion);
    }

    [Fact]
    public async Task Listar_AplicaFiltrosDeFechaRepartidorYEstado()
    {
        CrearRuta(new[] { CrearEnvio() }, fecha: Viernes);
        CrearRuta(new[] { CrearEnvio() }, fecha: Viernes.AddDays(1));
        CrearRuta(new[] { CrearEnvio() }, fecha: Viernes, repartidorId: Guid.NewGuid());
        CrearRuta(new[] { CrearEnvio() }, fecha: Viernes, estado: "EnCarga");

        Assert.Equal(4, (await HandlerListar().Handle(new ListarHojasDeRutaQuery(), default)).Count);
        Assert.Equal(3, (await HandlerListar().Handle(new ListarHojasDeRutaQuery(Fecha: Viernes), default)).Count);
        Assert.Equal(3, (await HandlerListar().Handle(new ListarHojasDeRutaQuery(RepartidorId: _repartidor.Id), default)).Count);
        Assert.Single(await HandlerListar().Handle(new ListarHojasDeRutaQuery(Estados: new[] { "EnCarga" }), default));
        Assert.Equal(2, (await HandlerListar().Handle(new ListarHojasDeRutaQuery(Fecha: Viernes, RepartidorId: _repartidor.Id, Estados: new[] { "Liberada", "EnCarga" }), default)).Count);
    }

    [Fact]
    public async Task Listar_NoIncluyeRutasDeOtrosOperadores()
    {
        CrearRuta(new[] { CrearEnvio() });
        CrearRuta(new[] { CrearEnvio(operadorId: Guid.NewGuid()) }, operadorId: Guid.NewGuid());

        var lista = await HandlerListar().Handle(new ListarHojasDeRutaQuery(), default);

        Assert.Single(lista);
    }

    [Fact]
    public async Task Listar_SinRutas_DevuelveListaVacia()
    {
        Assert.Empty(await HandlerListar().Handle(new ListarHojasDeRutaQuery(), default));
        await Assert.ThrowsAsync<ArgumentNullException>(() => HandlerListar().Handle(null!, default));
    }

    // ---- Detalle ----

    [Fact]
    public async Task Obtener_DevuelveParadasOrdenadasConVentanaYOmiteLasCanceladas()
    {
        var franja = new FranjaHoraria(Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(12, 0));
        _franjas.Franjas.Add(franja);
        var a = CrearEnvio(franja: franja.Id);
        var b = CrearEnvio();
        var c = CrearEnvio();
        var ruta = CrearRuta(new[] { a, b, c });
        ruta.DesasignarEnvioFaltante(c.Id);

        var detalle = await HandlerObtener().Handle(new ObtenerHojaDeRutaQuery(ruta.Id), default);

        Assert.Equal(2, detalle.Paradas.Count);
        Assert.Equal(new[] { 1, 2 }, detalle.Paradas.Select(p => p.Orden));
        Assert.Equal("09:00-12:00", detalle.Paradas[0].VentanaHoraria);
        Assert.Null(detalle.Paradas[1].VentanaHoraria);
        Assert.Equal(a.NombreDestinatario, detalle.Paradas[0].Destinatario);
        Assert.Equal(a.CodigoSeguimientoPublico, detalle.Paradas[0].CodigoSeguimiento);
        Assert.Equal(2, detalle.Ruta.CantidadParadas);
    }

    [Fact]
    public async Task Obtener_RutaDeOtroOperador_LanzaUnauthorizedAccess()
    {
        var ruta = CrearRuta(new[] { CrearEnvio() });
        _tenant.OperadorId = Guid.NewGuid();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => HandlerObtener().Handle(new ObtenerHojaDeRutaQuery(ruta.Id), default));
    }

    [Fact]
    public async Task Obtener_InexistenteIdVacioOConsultaNula_LanzaExcepcionesTipadas()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => HandlerObtener().Handle(new ObtenerHojaDeRutaQuery(Guid.NewGuid()), default));
        await Assert.ThrowsAsync<DomainValidationException>(() => HandlerObtener().Handle(new ObtenerHojaDeRutaQuery(Guid.Empty), default));
        await Assert.ThrowsAsync<ArgumentNullException>(() => HandlerObtener().Handle(null!, default));
    }

    // ---- Envíos pendientes ----

    [Fact]
    public async Task Pendientes_SoloDevuelveEnviosEnDepositoClasificadosDelOperador()
    {
        var listo = CrearEnvio();
        CrearEnvio(clasificar: false);
        CrearEnvio(recepcionar: false);
        CrearEnvio(operadorId: Guid.NewGuid());
        var asignado = CrearEnvio();
        CrearRuta(new[] { asignado });

        var handler = new ListarEnviosPendientesDeRutaQueryHandler(_envios, _tenant);
        var pendientes = await handler.Handle(new ListarEnviosPendientesDeRutaQuery(), default);

        var item = Assert.Single(pendientes);
        Assert.Equal(listo.Id, item.EnvioId);
        Assert.Equal("Alta", item.Prioridad);
        Assert.Equal(2m, item.PesoKg);
        Assert.Equal(listo.SelloModificacion, item.SelloModificacion);
    }

    // ---- Verificación de despacho ----

    [Fact]
    public async Task Verificacion_RutaConCargaCompleta_PuedeDespachar()
    {
        var a = CrearEnvio(bultos: 2);
        var ruta = CrearRuta(new[] { a }, estado: "EnCarga");

        var v = await HandlerVerificacion().Handle(new ObtenerVerificacionDespachoQuery(ruta.Id), default);

        Assert.True(v.PuedeDespachar);
        Assert.Equal(2, v.TotalBultos);
        Assert.Equal(2, v.BultosCargados);
        Assert.Equal(0, v.BultosDiscrepantes);
        Assert.False(v.TieneRutaPendienteDeRendir);
        Assert.Equal("SAB1234", v.VehiculoPatente);
    }

    [Fact]
    public async Task Verificacion_ConBultoNoCargado_LoInformaConSuMotivo()
    {
        var a = CrearEnvio(bultos: 2);
        var ruta = CrearRuta(new[] { a });
        ruta.RegistrarCargaEscaneada(new[] { a.Bultos.First().Id });

        var v = await HandlerVerificacion().Handle(new ObtenerVerificacionDespachoQuery(ruta.Id), default);

        Assert.Equal(1, v.BultosDiscrepantes);
        var faltante = v.Bultos.Single(b => !b.CargadoEfectivo);
        Assert.Contains("no fue escaneado", faltante.MotivoNoCarga);
        Assert.True(v.PuedeDespachar);
    }

    [Fact]
    public async Task Verificacion_SinCargaRegistrada_NoPuedeDespachar()
    {
        var ruta = CrearRuta(new[] { CrearEnvio() });

        var v = await HandlerVerificacion().Handle(new ObtenerVerificacionDespachoQuery(ruta.Id), default);

        Assert.False(v.PuedeDespachar);
        Assert.All(v.Bultos, b => Assert.Contains("Todavía no se registró", b.MotivoNoCarga));
    }

    [Fact]
    public async Task Verificacion_ConOtraRutaDelRepartidorSinRendir_NoPuedeDespacharEIndicaCual()
    {
        var previa = CrearRuta(new[] { CrearEnvio() }, estado: "Despachada");
        var ruta = CrearRuta(new[] { CrearEnvio() }, estado: "EnCarga");

        var v = await HandlerVerificacion().Handle(new ObtenerVerificacionDespachoQuery(ruta.Id), default);

        Assert.True(v.TieneRutaPendienteDeRendir);
        Assert.StartsWith("HR-", v.NumeroRutaPendienteRendicion);
        Assert.Contains(previa.Id.ToString()[..4].ToUpperInvariant(), v.NumeroRutaPendienteRendicion);
        Assert.False(v.PuedeDespachar);
    }

    [Fact]
    public async Task Verificacion_RutaDeOtroOperadorInexistenteOIdVacio_LanzaExcepcionesTipadas()
    {
        var ruta = CrearRuta(new[] { CrearEnvio() });

        await Assert.ThrowsAsync<NotFoundException>(() => HandlerVerificacion().Handle(new ObtenerVerificacionDespachoQuery(Guid.NewGuid()), default));
        await Assert.ThrowsAsync<DomainValidationException>(() => HandlerVerificacion().Handle(new ObtenerVerificacionDespachoQuery(Guid.Empty), default));
        _tenant.OperadorId = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => HandlerVerificacion().Handle(new ObtenerVerificacionDespachoQuery(ruta.Id), default));
    }

    // ---- Validación del armado (simulación) ----

    [Fact]
    public async Task ValidarArmado_NoAsignaNiPersisteNada()
    {
        _envios.ReglaOperativa = new ReglaOperativa(Guid.NewGuid(), _tenant.OperadorId, 1, DateTime.UtcNow.AddDays(-1), 3,
            TimeSpan.FromHours(24), "Firma", "Dev", TimeSpan.FromHours(48), TimeSpan.FromHours(24));
        var a = CrearEnvio();
        var uow = new FakeUnitOfWork();
        var handler = new ArmarHojaDeRutaCommandHandler(_rutas, _envios, _repartidores, _vehiculos, _franjas, uow, _tenant, new FakeClock());

        var resultado = await handler.Handle(
            new ArmarHojaDeRutaCommand(_repartidor.Id, _vehiculo.Id, Viernes, new[] { a.Id }, SoloValidar: true), default);

        Assert.Equal("Validada", resultado.Estado);
        Assert.Equal(Guid.Empty, resultado.HojaDeRutaId);
        Assert.Single(resultado.Paradas);
        Assert.Equal(EstadoEnvio.EnDeposito, a.EstadoActual);
        Assert.Null(a.HojaDeRutaId);
        Assert.Empty(_rutas.Rutas);
        Assert.Equal(0, uow.Confirmaciones);
    }
}
