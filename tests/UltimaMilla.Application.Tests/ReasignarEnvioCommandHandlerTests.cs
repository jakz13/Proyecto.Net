using UltimaMilla.Application.Commands.Planificacion.ReasignarEnvio;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class ReasignarEnvioCommandHandlerTests
{
    private static readonly DateOnly Viernes = new(2026, 10, 16);

    private readonly FakeRutaRepository _rutas = new();
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeVehiculoRepository _vehiculos = new();
    private readonly FakeFranjaHorariaRepository _franjas = new();
    private readonly FakeOutboxStorage _outbox = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();
    private readonly Vehiculo _vehiculo;
    private int _secuencia;

    public ReasignarEnvioCommandHandlerTests()
    {
        _vehiculo = new Vehiculo(Guid.NewGuid(), _tenant.OperadorId, "SAB1234", "Furgoneta", 100m, 10m);
        _vehiculos.Vehiculos.Add(_vehiculo);
        _envios.ReglaOperativa = new ReglaOperativa(Guid.NewGuid(), _tenant.OperadorId, 1, _clock.UtcNow.AddDays(-5), 3,
            TimeSpan.FromHours(24), "Firma", "Dev", TimeSpan.FromHours(48), TimeSpan.FromHours(24), null, 3);
        _envios.Motivos.Add(new MotivoNoEntrega(Guid.NewGuid(), _tenant.OperadorId, "RETORNO_SIN_INTENTO", "Retorno", esInterno: true));
        _envios.Motivos.Add(new MotivoNoEntrega(Guid.NewGuid(), _tenant.OperadorId, "AUSENTE", "Ausente"));
    }

    private ReasignarEnvioCommandHandler CrearHandler() =>
        new(_rutas, _envios, _vehiculos, _franjas, _outbox, _uow, _tenant, _clock);

    private Envio CrearEnvio(decimal peso = 5m, Guid? franja = null)
    {
        _secuencia++;
        var codigo = $"TRK-REAS{_secuencia:0000}";
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", codigo, Guid.NewGuid(),
            franja, null, "Rivera 10", "Ana", _tenant.OperadorId);
        envio.AgregarBulto($"{codigo}-B01", peso, 10m, 10m, 10m);
        envio.RecepcionarEnDeposito(new[] { $"{codigo}-B01" }, null, null, Guid.NewGuid());
        envio.ClasificarBultos(new[] { new ClasificacionBulto($"{codigo}-B01", PrioridadBulto.Normal) }, Guid.NewGuid());
        _envios.Envios.Add(envio);
        return envio;
    }

    private HojaDeRuta CrearRuta(string estado, params Envio[] envios)
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, Viernes);
        ruta.AsignarRecursos(Guid.NewGuid(), _vehiculo.Id);
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
        {
            ruta.Despachar();
            foreach (var envio in envios)
                envio.DespacharEnRuta(ruta.Id);
        }

        _rutas.Rutas.Add(ruta);
        return ruta;
    }

    private Task<ReasignacionResultDto> Ejecutar(HojaDeRuta ruta, Envio envio, string accion = "Retirar", Guid? destino = null,
        Guid? franja = null, string? motivo = null, string? obs = null) =>
        CrearHandler().Handle(new ReasignarEnvioCommand(ruta.Id, envio.Id, accion, destino, franja, motivo, obs), default);

    [Fact]
    public async Task Retirar_RutaLiberada_DevuelveElEnvioADepositoYLiberaLaAsignacion()
    {
        var a = CrearEnvio();
        var b = CrearEnvio();
        var ruta = CrearRuta("Liberada", a, b);

        var resultado = await Ejecutar(ruta, a, obs: "Repartidor ausente");

        Assert.Equal("EnDeposito", resultado.EstadoEnvio);
        Assert.False(resultado.PendienteDeRendicion);
        Assert.Null(a.HojaDeRutaId);
        Assert.Equal("RetiroDeRuta", a.Eventos.Last().TipoEvento);
        Assert.Contains("Repartidor ausente", a.Eventos.Last().Payload);
        Assert.Equal("Cancelada", ruta.Paradas.Single(p => p.EnvioId == a.Id).Estado);
        Assert.False(ruta.Asignaciones.Single(x => x.EnvioId == a.Id).Vigente);
        Assert.Equal(EstadoEnvio.AsignadoARuta, b.EstadoActual);
        Assert.Null(a.ObtenerMotivoNoAsignable());
        Assert.Single(_outbox.Mensajes, m => m.TipoEvento == "EnvioRetiradoDeRutaIntegrationEvent");
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task Retirar_RutaEnCarga_TambienVuelveADeposito()
    {
        var a = CrearEnvio();
        var b = CrearEnvio();
        var ruta = CrearRuta("EnCarga", a, b);

        await Ejecutar(ruta, a);

        Assert.Equal(EstadoEnvio.EnDeposito, a.EstadoActual);
    }

    [Fact]
    public async Task ReasignarARuta_AgregaLaParadaAlFinalDeLaRutaDestino()
    {
        var a = CrearEnvio();
        var origen = CrearRuta("Liberada", a);
        var destino = CrearRuta("Liberada", CrearEnvio(), CrearEnvio());

        var resultado = await Ejecutar(origen, a, "ReasignarARuta", destino.Id);

        Assert.Equal(destino.Id, resultado.HojaDeRutaDestinoId);
        Assert.Equal(EstadoEnvio.AsignadoARuta, a.EstadoActual);
        Assert.Equal(destino.Id, a.HojaDeRutaId);
        var parada = destino.Paradas.Single(p => p.EnvioId == a.Id);
        Assert.Equal(3, parada.Orden);
        Assert.True(destino.Asignaciones.Single(x => x.EnvioId == a.Id).Vigente);
        Assert.Equal("Cancelada", origen.Paradas.Single().Estado);
        Assert.Equal(new[] { "RetiroDeRuta", "AsignacionRuta" }, a.Eventos.TakeLast(2).Select(e => e.TipoEvento));
    }

    [Theory]
    [InlineData("EnCarga")]
    [InlineData("Despachada")]
    public async Task ReasignarARuta_DestinoNoLiberada_SeRechazaSinMoverElEnvio(string estadoDestino)
    {
        var a = CrearEnvio();
        var origen = CrearRuta("Liberada", a);
        var destino = CrearRuta(estadoDestino, CrearEnvio());

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(origen, a, "ReasignarARuta", destino.Id));

        Assert.Equal(EstadoEnvio.AsignadoARuta, a.EstadoActual);
        Assert.Equal(origen.Id, a.HojaDeRutaId);
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task ReasignarARuta_SuperaLaCapacidadDelVehiculo_SeRechazaSinMoverElEnvio()
    {
        var pesado = CrearEnvio(peso: 60m);
        var origen = CrearRuta("Liberada", pesado);
        var destino = CrearRuta("Liberada", CrearEnvio(peso: 60m));

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(origen, pesado, "ReasignarARuta", destino.Id));

        Assert.Contains("capacidad de peso", ex.Message);
        Assert.Equal(EstadoEnvio.AsignadoARuta, pesado.EstadoActual);
        Assert.Equal("Pendiente", origen.Paradas.Single().Estado);
    }

    [Fact]
    public async Task ReasignarARuta_SuperaLasParadasMaximas_SeRechaza()
    {
        var a = CrearEnvio();
        var origen = CrearRuta("Liberada", a);
        var destino = CrearRuta("Liberada", CrearEnvio(), CrearEnvio(), CrearEnvio());

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(origen, a, "ReasignarARuta", destino.Id));

        Assert.Contains("máximo de paradas", ex.Message);
    }

    [Fact]
    public async Task ReasignarARuta_FranjaIncompatibleConElDiaDeLaRuta_SeRechaza()
    {
        var franja = new FranjaHoraria(Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0));
        _franjas.Franjas.Add(franja);
        var a = CrearEnvio(franja: franja.Id);
        var origen = CrearRuta("Liberada", a);
        var destino = CrearRuta("Liberada", CrearEnvio());

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(origen, a, "ReasignarARuta", destino.Id));

        Assert.Contains("franja", ex.Message);
    }

    [Fact]
    public async Task ReasignarARuta_DestinoInvalido_LanzaExcepcionesTipadas()
    {
        var a = CrearEnvio();
        var origen = CrearRuta("Liberada", a);

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(origen, a, "ReasignarARuta"));
        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(origen, a, "ReasignarARuta", origen.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => Ejecutar(origen, a, "ReasignarARuta", Guid.NewGuid()));
        Assert.Equal(EstadoEnvio.AsignadoARuta, a.EstadoActual);
    }

    [Fact]
    public async Task ReasignarARuta_DestinoDeOtroOperador_LanzaUnauthorizedAccess()
    {
        var a = CrearEnvio();
        var origen = CrearRuta("Liberada", a);
        var ajena = new HojaDeRuta(Guid.NewGuid(), Guid.NewGuid(), Viernes);
        _rutas.Rutas.Add(ajena);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Ejecutar(origen, a, "ReasignarARuta", ajena.Id));
        Assert.Equal(EstadoEnvio.AsignadoARuta, a.EstadoActual);
    }

    [Fact]
    public async Task Reprogramar_CambiaLaFranjaYDejaElEnvioEnDeposito()
    {
        var franja = new FranjaHoraria(Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Saturday, new TimeOnly(14, 0), new TimeOnly(18, 0));
        _franjas.Franjas.Add(franja);
        var a = CrearEnvio();
        var ruta = CrearRuta("Liberada", a);

        var resultado = await Ejecutar(ruta, a, "Reprogramar", franja: franja.Id);

        Assert.Equal("EnDeposito", resultado.EstadoEnvio);
        Assert.Equal(franja.Id, a.FranjaHorariaComprometidaId);
        Assert.Equal("ReprogramacionFranja", a.Eventos.Last().TipoEvento);
    }

    [Fact]
    public async Task Reprogramar_FranjaInactivaInexistenteOVacia_SeRechazaSinMoverElEnvio()
    {
        var inactiva = new FranjaHoraria(Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Saturday, new TimeOnly(14, 0), new TimeOnly(18, 0));
        inactiva.Desactivar();
        _franjas.Franjas.Add(inactiva);
        var a = CrearEnvio();
        var ruta = CrearRuta("Liberada", a);

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(ruta, a, "Reprogramar", franja: inactiva.Id));
        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(ruta, a, "Reprogramar", franja: Guid.NewGuid()));
        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(ruta, a, "Reprogramar"));
        Assert.Equal(EstadoEnvio.AsignadoARuta, a.EstadoActual);
    }

    [Fact]
    public async Task RutaDespachada_RetiraElEnvioComoNoEntregadoConMotivoInterno()
    {
        var a = CrearEnvio();
        var b = CrearEnvio();
        var ruta = CrearRuta("Despachada", a, b);

        var resultado = await Ejecutar(ruta, a, motivo: "RETORNO_SIN_INTENTO", obs: "Avería del vehículo");

        Assert.True(resultado.PendienteDeRendicion);
        Assert.Equal(EstadoEnvio.NoEntregado, a.EstadoActual);
        Assert.Equal(ruta.Id, a.HojaDeRutaId);
        Assert.Equal("RetiroEnRuta", a.Eventos.Last().TipoEvento);
        Assert.Equal("Cancelada", ruta.Paradas.Single(p => p.EnvioId == a.Id).Estado);
        Assert.Equal(EstadoEnvio.EnTransito, b.EstadoActual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NO_EXISTE")]
    [InlineData("AUSENTE")]
    public async Task RutaDespachada_SinMotivoInternoValido_SeRechazaSinCambios(string? motivo)
    {
        var a = CrearEnvio();
        var ruta = CrearRuta("Despachada", a);

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(ruta, a, motivo: motivo));

        Assert.Equal(EstadoEnvio.EnTransito, a.EstadoActual);
        Assert.Equal("Pendiente", ruta.Paradas.Single().Estado);
    }

    [Theory]
    [InlineData("ReasignarARuta")]
    [InlineData("Reprogramar")]
    public async Task RutaDespachada_NoPermiteReasignarNiReprogramarDeInmediato(string accion)
    {
        var a = CrearEnvio();
        var ruta = CrearRuta("Despachada", a);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(ruta, a, accion, motivo: "RETORNO_SIN_INTENTO"));

        Assert.Contains("después de la rendición", ex.Message);
    }

    [Fact]
    public async Task RutaDespachada_ParadaYaCerrada_NoSePuedeRetirar()
    {
        var a = CrearEnvio();
        var b = CrearEnvio();
        var ruta = CrearRuta("Despachada", a, b);
        a.RegistrarEntregaExitosa(ruta.Paradas.First().Id, _envios.ReglaOperativa!, "Juan", "1", "firma", null, null, null, DateTime.UtcNow);
        ruta.RegistrarEntregaExitosaEnParada(ruta.Paradas.First().Id);

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(ruta, a, motivo: "RETORNO_SIN_INTENTO"));
    }

    [Theory]
    [InlineData("Borrar")]
    [InlineData("  ")]
    public async Task Accion_Invalida_SeRechaza(string accion)
    {
        var a = CrearEnvio();
        var ruta = CrearRuta("Liberada", a);

        if (string.IsNullOrWhiteSpace(accion))
        {
            // Sin acción se interpreta como retirar
            var resultado = await Ejecutar(ruta, a, accion);
            Assert.Equal("Retirar", resultado.Accion);
        }
        else
        {
            await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(ruta, a, accion));
        }
    }

    [Fact]
    public async Task EnvioDeOtraRuta_SeRechaza()
    {
        var a = CrearEnvio();
        var otro = CrearEnvio();
        var ruta = CrearRuta("Liberada", a);
        CrearRuta("Liberada", otro);

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(ruta, otro));
    }

    [Fact]
    public async Task EnviosOrutasInexistentesOVacios_LanzanExcepcionesTipadas()
    {
        var a = CrearEnvio();
        var ruta = CrearRuta("Liberada", a);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CrearHandler().Handle(new ReasignarEnvioCommand(Guid.NewGuid(), a.Id), default));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CrearHandler().Handle(new ReasignarEnvioCommand(ruta.Id, Guid.NewGuid()), default));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(new ReasignarEnvioCommand(Guid.Empty, a.Id), default));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(new ReasignarEnvioCommand(ruta.Id, Guid.Empty), default));
        await Assert.ThrowsAsync<ArgumentNullException>(() => CrearHandler().Handle(null!, default));
    }

    [Fact]
    public async Task RutaOEnvioDeOtroOperador_LanzaUnauthorizedAccess()
    {
        var a = CrearEnvio();
        var ruta = CrearRuta("Liberada", a);
        _tenant.OperadorId = Guid.NewGuid();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Ejecutar(ruta, a));
        Assert.Equal(EstadoEnvio.AsignadoARuta, a.EstadoActual);
    }

    [Fact]
    public async Task UsuarioNoIdentificado_LanzaUnauthorizedAccess()
    {
        var a = CrearEnvio();
        var ruta = CrearRuta("Liberada", a);
        _tenant.UsuarioId = null;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Ejecutar(ruta, a));
    }
}
