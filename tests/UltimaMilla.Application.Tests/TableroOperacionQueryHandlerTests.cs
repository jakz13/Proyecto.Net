using System.Reflection;
using UltimaMilla.Application.Commands.Tablero.EnviarInstruccionRepartidor;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Tablero;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Exceptions;
using Xunit;

namespace UltimaMilla.Application.Tests;

public class TableroOperacionQueryHandlerTests
{
    private static readonly DateOnly Viernes = new(2026, 10, 16);

    private readonly FakeRutaRepository _rutas = new();
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeRepartidorRepository _repartidores = new();
    private readonly FakeVehiculoRepository _vehiculos = new();
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeDireccionRepository _direcciones = new();
    private readonly FakeFranjaHorariaRepository _franjas = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();
    private readonly FakeOutboxStorage _outbox = new();
    private readonly FakeUnitOfWork _uow = new();

    private readonly Repartidor _repartidor;
    private readonly Vehiculo _vehiculo;
    private int _secuencia;

    public TableroOperacionQueryHandlerTests()
    {
        var usuario = new Domain.Entities.Identidad.Usuario(
            Guid.NewGuid(),
            "Juan Perez",
            "juan.perez@ultimamilla.com.uy",
            "hash",
            perfilId: Guid.NewGuid(),
            operadorId: _tenant.OperadorId);
        _usuarios.Usuarios.Add(usuario);

        _repartidor = new Repartidor(Guid.NewGuid(), usuario.Id, _tenant.OperadorId, "1.111.111-1", "099123456");
        _vehiculo = new Vehiculo(Guid.NewGuid(), _tenant.OperadorId, "STX1021", "Renault Kangoo", 500m, 10m);

        _repartidores.Repartidores.Add(_repartidor);
        _vehiculos.Vehiculos.Add(_vehiculo);

        // Fijar hora del reloj en el viernes a las 11:00 UTC (08:00 hora de Montevideo)
        _clock.UtcNow = new DateTime(2026, 10, 16, 11, 0, 0, DateTimeKind.Utc);
    }

    private ObtenerTableroOperacionQueryHandler CrearHandler() =>
        new(_rutas, _envios, _vehiculos, _repartidores, _usuarios, _direcciones, _franjas, _tenant, _clock);

    private Envio CrearEnvio(Guid? direccionId = null, Guid? franjaId = null, Guid? operadorId = null)
    {
        _secuencia++;
        var codigo = $"ENV-2026-{_secuencia:0000}";
        var envio = new Envio(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Estandar",
            codigo,
            direccionEntregaId: direccionId,
            franjaHorariaComprometidaId: franjaId,
            loteImportacionId: null,
            direccionDestino: $"Calle {_secuencia} 123",
            nombreDestinatario: $"Destinatario {_secuencia}",
            operadorId: operadorId ?? _tenant.OperadorId);

        _envios.Envios.Add(envio);
        return envio;
    }

    private HojaDeRuta CrearRuta(string estado = "Despachada", DateOnly? fecha = null, Guid? operadorId = null)
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), operadorId ?? _tenant.OperadorId, fecha ?? Viernes, estado: estado);
        ruta.AsignarRecursos(_repartidor.Id, _vehiculo.Id);
        _rutas.Rutas.Add(ruta);
        return ruta;
    }

    // =========================================================================
    // CASOS NO FELICES Y RESILIENCIA: VALIDACIONES Y ENTRADAS ANÓMALAS
    // =========================================================================

    [Fact]
    public async Task Handle_RequestNulo_LanzaArgumentNullException()
    {
        var handler = CrearHandler();
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, default));
    }

    [Fact]
    public async Task Handle_SinRutasEnEjecucion_DevuelveTotalesEnCeroYListasVacias()
    {
        var handler = CrearHandler();

        // Ruta en Borrador no debe considerarse en ejecución
        var rutaBorrador = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, Viernes, estado: "Borrador");
        _rutas.Rutas.Add(rutaBorrador);

        var resultado = await handler.Handle(new ObtenerTableroOperacionQuery(), default);

        Assert.Equal(0, resultado.TotalRutasEnEjecucion);
        Assert.Equal(0, resultado.TotalRepartidoresEnCalle);
        Assert.Equal(0, resultado.TotalEnviosEnTransito);
        Assert.Equal(0, resultado.TotalEnviosEntregados);
        Assert.Equal(0, resultado.TotalEnviosEnRiesgo);
        Assert.Empty(resultado.Vehiculos);
        Assert.Empty(resultado.AlertasRiesgo);
    }

    [Fact]
    public async Task Handle_RutasDeOtroOperador_AislamientoMultitenant_NoSeDevuelven()
    {
        var handler = CrearHandler();
        var otroOperador = Guid.NewGuid();

        var rutaAjena = new HojaDeRuta(Guid.NewGuid(), otroOperador, Viernes, estado: "Despachada");
        rutaAjena.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        _rutas.Rutas.Add(rutaAjena);

        var resultado = await handler.Handle(new ObtenerTableroOperacionQuery(), default);

        Assert.Equal(0, resultado.TotalRutasEnEjecucion);
        Assert.Empty(resultado.Vehiculos);
    }

    [Fact]
    public async Task Handle_RutaDespachadaSinRecursosAsignados_SeOmiteSinCrashear()
    {
        var handler = CrearHandler();

        // Hoja de ruta anómala sin repartidor/vehículo
        var rutaSinRecursos = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, Viernes, estado: "Despachada");
        _rutas.Rutas.Add(rutaSinRecursos);

        var resultado = await handler.Handle(new ObtenerTableroOperacionQuery(), default);

        Assert.Equal(0, resultado.TotalRutasEnEjecucion);
        Assert.Empty(resultado.Vehiculos);
    }

    // =========================================================================
    // DETECCIÓN DE RIESGOS DE VENTANA COMPROMETIDA (RF-29)
    // =========================================================================

    [Fact]
    public async Task Handle_EnvioConVentanaVencida_SeMarcaComoRiesgoCritico()
    {
        var handler = CrearHandler();

        // Hora fijada a las 15:00 UTC (12:00 hora de Montevideo)
        _clock.UtcNow = new DateTime(2026, 10, 16, 15, 0, 0, DateTimeKind.Utc);

        // Franja de 09:00 a 11:00 (venció hace 1 hora respecto a las 12:00)
        var franjaVencida = new FranjaHoraria(
            Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(11, 0));
        _franjas.Franjas.Add(franjaVencida);

        var envio = CrearEnvio(franjaId: franjaVencida.Id);
        var ruta = CrearRuta("Despachada");
        ruta.AgregarParada(Guid.NewGuid(), envio.Id, 1, Guid.NewGuid(), franjaVencida.Id, "Pendiente");

        var resultado = await handler.Handle(new ObtenerTableroOperacionQuery(), default);

        Assert.Equal(1, resultado.TotalEnviosEnRiesgo);
        var alerta = Assert.Single(resultado.AlertasRiesgo);
        Assert.Equal("Critico", alerta.NivelRiesgo);
        Assert.Equal(envio.CodigoSeguimientoPublico, alerta.CodigoSeguimiento);
        Assert.True(alerta.MinutosParaCierreVentana < 0);
        Assert.True(resultado.Vehiculos[0].TieneAlertasRiesgo);
    }

    [Fact]
    public async Task Handle_EnvioConTiempoSuficiente_NoGeneraAlerta()
    {
        var handler = CrearHandler();

        // Hora local = 08:00 Montevideo. Franja de 14:00 a 18:00 (faltan 10 horas)
        var franjaHolgada = new FranjaHoraria(
            Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Friday, new TimeOnly(14, 0), new TimeOnly(18, 0));
        _franjas.Franjas.Add(franjaHolgada);

        var envio = CrearEnvio(franjaId: franjaHolgada.Id);
        var ruta = CrearRuta("Despachada");
        ruta.AgregarParada(Guid.NewGuid(), envio.Id, 1, Guid.NewGuid(), franjaHolgada.Id, "Pendiente");

        var resultado = await handler.Handle(new ObtenerTableroOperacionQuery(), default);

        Assert.Equal(0, resultado.TotalEnviosEnRiesgo);
        Assert.Empty(resultado.AlertasRiesgo);
        Assert.False(resultado.Vehiculos[0].TieneAlertasRiesgo);
    }

    [Fact]
    public async Task Handle_FranjaDeOtroDiaDeLaSemana_NoGeneraAlertaInvalida()
    {
        var handler = CrearHandler();

        // Franja configurada para los lunes, pero la ruta es viernes
        var franjaLunes = new FranjaHoraria(
            Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(9, 0));
        _franjas.Franjas.Add(franjaLunes);

        var envio = CrearEnvio(franjaId: franjaLunes.Id);
        var ruta = CrearRuta("Despachada");
        ruta.AgregarParada(Guid.NewGuid(), envio.Id, 1, Guid.NewGuid(), franjaLunes.Id, "Pendiente");

        var resultado = await handler.Handle(new ObtenerTableroOperacionQuery(), default);

        Assert.Equal(0, resultado.TotalEnviosEnRiesgo);
    }

    // =========================================================================
    // UBICACIÓN Y TELEMETRÍA DE VEHÍCULOS
    // =========================================================================

    [Fact]
    public async Task Handle_RepartidorEnSitio_AdoptaCoordenadasDeLaParada()
    {
        var handler = CrearHandler();

        var dir = new Direccion(Guid.NewGuid(), "Av. Brasil", "2890", "Montevideo", null, -34.9150, -56.1520);
        _direcciones.Direcciones.Add(dir);

        var envio = CrearEnvio(direccionId: dir.Id);
        var ruta = CrearRuta("Despachada");
        ruta.AgregarParada(Guid.NewGuid(), envio.Id, 1, dir.Id, null, "EnSitio");

        var resultado = await handler.Handle(new ObtenerTableroOperacionQuery(), default);

        var vehiculo = Assert.Single(resultado.Vehiculos);
        Assert.Equal(-34.9150, vehiculo.Latitud);
        Assert.Equal(-56.1520, vehiculo.Longitud);
        Assert.Equal("EnParada", vehiculo.EstadoOperativo);
        Assert.Equal("EnParada", vehiculo.OrigenPosicion);
    }

    [Fact]
    public async Task Handle_SinParadaEnSitio_AdoptaCoordenadasDeUltimoIntentoEntrega()
    {
        var handler = CrearHandler();

        var envio = CrearEnvio();
        var ruta = CrearRuta("Despachada");
        var paradaId = Guid.NewGuid();
        ruta.AgregarParada(paradaId, envio.Id, 1, Guid.NewGuid(), null, "Pendiente");

        // Inyectar un intento de entrega con geolocalización previa
        var intento = new IntentoEntrega(
            Guid.NewGuid(), paradaId, envio.Id, "Fallido", "MobileApp",
            motivoNoEntregaId: Guid.NewGuid(), latitud: -34.8900, longitud: -56.1800);

        var campo = typeof(Envio).GetField("_intentosEntrega", BindingFlags.NonPublic | BindingFlags.Instance)!;
        ((List<IntentoEntrega>)campo.GetValue(envio)!).Add(intento);

        var resultado = await handler.Handle(new ObtenerTableroOperacionQuery(), default);

        var vehiculo = Assert.Single(resultado.Vehiculos);
        Assert.Equal(-34.8900, vehiculo.Latitud);
        Assert.Equal(-56.1800, vehiculo.Longitud);
        Assert.Equal("UltimoIntentoDeEntrega", vehiculo.OrigenPosicion);
    }

    [Fact]
    public async Task Handle_SinTelemetriaNiIntentos_LatitudYLongitudSonNulas()
    {
        var handler = CrearHandler();

        var envio = CrearEnvio();
        var ruta = CrearRuta("Despachada");
        ruta.AgregarParada(Guid.NewGuid(), envio.Id, 1, Guid.NewGuid(), null, "Pendiente");

        var resultado = await handler.Handle(new ObtenerTableroOperacionQuery(), default);

        var vehiculo = Assert.Single(resultado.Vehiculos);
        Assert.Null(vehiculo.Latitud);
        Assert.Null(vehiculo.Longitud);
        Assert.Null(vehiculo.OrigenPosicion);
    }

    // =========================================================================
    // PRUEBAS DE COMANDO: ENVIAR INSTRUCCIÓN AL REPARTIDOR
    // =========================================================================

    [Fact]
    public async Task EnviarInstruccion_ComandoValido_GuardaEventoOutboxYRetornaTrue()
    {
        var handler = new EnviarInstruccionRepartidorCommandHandler(
            _repartidores, _outbox, _uow, _tenant, _clock);

        var command = new EnviarInstruccionRepartidorCommand(
            _repartidor.Id, "Priorizar parada de Av. Brasil para evitar caída de SLA.");

        var resultado = await handler.Handle(command, default);

        Assert.True(resultado);
        Assert.Single(_outbox.Mensajes);
        var msg = _outbox.Mensajes[0];
        Assert.Equal("InstruccionOperativaEnviadaIntegrationEvent", msg.TipoEvento);
        Assert.Contains("Priorizar parada", msg.Payload);
    }

    [Fact]
    public async Task EnviarInstruccion_RepartidorVacio_LanzaDomainValidationException()
    {
        var handler = new EnviarInstruccionRepartidorCommandHandler(
            _repartidores, _outbox, _uow, _tenant, _clock);

        var command = new EnviarInstruccionRepartidorCommand(Guid.Empty, "Mensaje válido");

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(command, default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EnviarInstruccion_MensajeVacio_LanzaDomainValidationException(string? mensaje)
    {
        var handler = new EnviarInstruccionRepartidorCommandHandler(
            _repartidores, _outbox, _uow, _tenant, _clock);

        var command = new EnviarInstruccionRepartidorCommand(_repartidor.Id, mensaje!);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(command, default));
    }

    [Fact]
    public async Task EnviarInstruccion_RepartidorInexistente_LanzaNotFoundException()
    {
        var handler = new EnviarInstruccionRepartidorCommandHandler(
            _repartidores, _outbox, _uow, _tenant, _clock);

        var command = new EnviarInstruccionRepartidorCommand(Guid.NewGuid(), "Mensaje");

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, default));
    }

    [Fact]
    public async Task EnviarInstruccion_RepartidorDeOtroOperador_LanzaUnauthorizedAccessException()
    {
        var handler = new EnviarInstruccionRepartidorCommandHandler(
            _repartidores, _outbox, _uow, _tenant, _clock);

        var ajeno = new Repartidor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "2.222.222-2", "099000000");
        _repartidores.Repartidores.Add(ajeno);

        var command = new EnviarInstruccionRepartidorCommand(ajeno.Id, "Mensaje");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, default));
    }

    // =========================================================================
    // EVALUADOR RIESGO VENTANA (UNIT TESTS)
    // =========================================================================

    [Theory]
    [InlineData(-1, "Critico")]
    [InlineData(-60, "Critico")]
    [InlineData(5, "Alto")]      // 5 min restantes cuando se necesitan 15 min (1 parada) -> 5*2=10 < 15 -> Alto
    [InlineData(10, "Moderado")] // 10 min restantes cuando se necesitan 15 min -> 10*2=20 >= 15 -> Moderado
    [InlineData(20, null)]       // 20 min restantes cuando se necesitan 15 min -> Llega a tiempo
    public void EvaluadorRiesgoVentana_CalculoMargen_DeterminaNivelCorrecto(int minutosMargen, string? nivelEsperado)
    {
        var ahora = new DateTime(2026, 10, 16, 10, 0, 0);
        var cierre = ahora.AddMinutes(minutosMargen);

        var resultado = EvaluadorRiesgoVentana.Evaluar(ahora, cierre, paradasPendientesHastaEsta: 1);

        if (nivelEsperado is null)
        {
            Assert.Null(resultado);
        }
        else
        {
            Assert.NotNull(resultado);
            Assert.Equal(nivelEsperado, resultado.Nivel);
        }
    }
}
