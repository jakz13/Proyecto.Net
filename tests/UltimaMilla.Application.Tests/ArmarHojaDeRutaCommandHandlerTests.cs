using UltimaMilla.Application.Commands.Planificacion.ArmarHojaDeRuta;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class ArmarHojaDeRutaCommandHandlerTests
{
    // El reloj fijo es jueves 15/10/2026; la jornada de las rutas es el viernes 16/10/2026
    private static readonly DateOnly Viernes = new(2026, 10, 16);

    private readonly FakeRutaRepository _rutas = new();
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeRepartidorRepository _repartidores = new();
    private readonly FakeVehiculoRepository _vehiculos = new();
    private readonly FakeFranjaHorariaRepository _franjas = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();
    private int _secuencia;

    private readonly Repartidor _repartidor;
    private readonly Vehiculo _vehiculo;

    public ArmarHojaDeRutaCommandHandlerTests()
    {
        _repartidor = new Repartidor(Guid.NewGuid(), Guid.NewGuid(), _tenant.OperadorId, "1.111.111-1", "099111111");
        _vehiculo = new Vehiculo(Guid.NewGuid(), _tenant.OperadorId, "SAB1234", "Furgoneta", capacidadPeso: 100m, capacidadVolumen: 10000m);
        _repartidores.Repartidores.Add(_repartidor);
        _vehiculos.Vehiculos.Add(_vehiculo);
        _envios.ReglaOperativa = CrearRegla();
    }

    private ArmarHojaDeRutaCommandHandler CrearHandler() =>
        new(_rutas, _envios, _repartidores, _vehiculos, _franjas, _uow, _tenant, _clock);

    private ReglaOperativa CrearRegla(int maxParadas = 50) =>
        new(Guid.NewGuid(), _tenant.OperadorId, 1, _clock.UtcNow.AddDays(-10), 3, TimeSpan.FromHours(24),
            "Firma", "Devolución a depósito", TimeSpan.FromHours(48), TimeSpan.FromHours(24), null, maxParadas);

    /// <summary>Crea un envío recepcionado, clasificado y listo para asignarse a una ruta.</summary>
    private Envio CrearEnvioListo(
        decimal peso = 5m,
        PrioridadBulto prioridad = PrioridadBulto.Normal,
        Guid? zona = null,
        Guid? franja = null,
        Guid? operadorId = null,
        string? codigo = null,
        bool clasificar = true,
        bool recepcionar = true)
    {
        _secuencia++;
        codigo ??= $"TRK-RUTA{_secuencia:0000}";
        var envio = new Envio(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", codigo,
            direccionEntregaId: Guid.NewGuid(),
            franjaHorariaComprometidaId: franja,
            operadorId: operadorId ?? _tenant.OperadorId);

        var identificacion = $"{codigo}-B01";
        envio.AgregarBulto(identificacion, peso, 10m, 10m, 10m);

        if (recepcionar)
            envio.RecepcionarEnDeposito(new[] { identificacion }, null, null, Guid.NewGuid());

        if (recepcionar && clasificar)
            envio.ClasificarBultos(new[] { new ClasificacionBulto(identificacion, prioridad, zona) }, Guid.NewGuid());

        _envios.Envios.Add(envio);
        return envio;
    }

    private FranjaHoraria CrearFranja(DayOfWeek dia = DayOfWeek.Friday, int horaInicio = 9, bool activa = true)
    {
        var franja = new FranjaHoraria(Guid.NewGuid(), Guid.NewGuid(), dia, new TimeOnly(horaInicio, 0), new TimeOnly(horaInicio + 3, 0));
        if (!activa)
            franja.Desactivar();
        _franjas.Franjas.Add(franja);
        return franja;
    }

    private ArmarHojaDeRutaCommand Comando(IEnumerable<Envio> envios, DateOnly? fecha = null) =>
        new(_repartidor.Id, _vehiculo.Id, fecha ?? Viernes, envios.Select(e => e.Id).ToList());

    private Task<ArmarHojaDeRutaResultDto> Ejecutar(ArmarHojaDeRutaCommand comando) =>
        CrearHandler().Handle(comando, CancellationToken.None);

    private void AssertNadaPersistido()
    {
        Assert.Empty(_rutas.Rutas);
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task ArmadoValido_LiberaLaRutaYAsignaLosEnvios()
    {
        var a = CrearEnvioListo();
        var b = CrearEnvioListo();

        var resultado = await Ejecutar(Comando(new[] { a, b }));

        var ruta = Assert.Single(_rutas.Rutas);
        Assert.Equal(ruta.Id, resultado.HojaDeRutaId);
        Assert.Equal("Liberada", resultado.Estado);
        Assert.Equal("Liberada", ruta.Estado);
        Assert.NotNull(ruta.FechaLiberacion);
        Assert.Equal(_repartidor.Id, ruta.RepartidorId);
        Assert.Equal(_vehiculo.Id, ruta.VehiculoId);
        Assert.Equal(Viernes, ruta.Fecha);
        Assert.Equal(2, ruta.Paradas.Count);
        Assert.Equal(2, ruta.Asignaciones.Count(x => x.Vigente));
        Assert.All(new[] { a, b }, e =>
        {
            Assert.Equal(EstadoEnvio.AsignadoARuta, e.EstadoActual);
            Assert.Equal(ruta.Id, e.HojaDeRutaId);
            Assert.Equal(_tenant.UsuarioId, e.Eventos.Last().ResponsableUsuarioId);
        });
        Assert.Empty(resultado.Excluidos);
        Assert.Equal(10m, resultado.PesoTotal);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task Paradas_SeOrdenanPorFranjaLuegoPrioridadLuegoZonaYLuegoCodigo()
    {
        var manana = CrearFranja(horaInicio: 8);
        var tarde = CrearFranja(horaInicio: 14);
        var zonaB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000000");
        var zonaA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000000");

        var sinFranjaUrgente = CrearEnvioListo(prioridad: PrioridadBulto.Urgente, codigo: "TRK-F");
        var tardeNormal = CrearEnvioListo(franja: tarde.Id, codigo: "TRK-E");
        var mananaBaja = CrearEnvioListo(franja: manana.Id, prioridad: PrioridadBulto.Baja, codigo: "TRK-D");
        var mananaUrgenteZonaB = CrearEnvioListo(franja: manana.Id, prioridad: PrioridadBulto.Urgente, zona: zonaB, codigo: "TRK-C");
        var mananaUrgenteZonaA = CrearEnvioListo(franja: manana.Id, prioridad: PrioridadBulto.Urgente, zona: zonaA, codigo: "TRK-B");
        var mananaUrgenteZonaAOtro = CrearEnvioListo(franja: manana.Id, prioridad: PrioridadBulto.Urgente, zona: zonaA, codigo: "TRK-A");

        var resultado = await Ejecutar(Comando(new[]
        {
            sinFranjaUrgente, tardeNormal, mananaBaja, mananaUrgenteZonaB, mananaUrgenteZonaA, mananaUrgenteZonaAOtro
        }));

        var codigos = resultado.Paradas.Select(p => p.CodigoSeguimiento).ToList();
        Assert.Equal(new[] { "TRK-A", "TRK-B", "TRK-C", "TRK-D", "TRK-E", "TRK-F" }, codigos);
        Assert.Equal(Enumerable.Range(1, 6), resultado.Paradas.Select(p => p.Orden));
    }

    [Fact]
    public async Task EnviosRepetidosEnLaSeleccion_SeAsignanUnaSolaVez()
    {
        var a = CrearEnvioListo();

        var resultado = await Ejecutar(new ArmarHojaDeRutaCommand(_repartidor.Id, _vehiculo.Id, Viernes, new[] { a.Id, a.Id, a.Id }));

        Assert.Single(resultado.Paradas);
        Assert.Empty(resultado.Excluidos);
        Assert.Single(a.Eventos, e => e.TipoEvento == "AsignacionRuta");
        Assert.Single(_rutas.Rutas.Single().Asignaciones);
    }

    [Fact]
    public async Task EnvioInexistenteODeOtroOperador_SeExcluyeComoInexistente()
    {
        var valido = CrearEnvioListo();
        var ajeno = CrearEnvioListo(operadorId: Guid.NewGuid());
        var fantasma = Guid.NewGuid();

        var resultado = await Ejecutar(new ArmarHojaDeRutaCommand(_repartidor.Id, _vehiculo.Id, Viernes, new[] { valido.Id, ajeno.Id, fantasma }));

        Assert.Single(resultado.Paradas);
        Assert.Equal(2, resultado.Excluidos.Count);
        Assert.All(resultado.Excluidos, e => Assert.Equal("El envío no existe.", e.Motivo));
        Assert.Equal(EstadoEnvio.EnDeposito, ajeno.EstadoActual);
    }

    [Fact]
    public async Task EnvioSinRecepcionarOSinClasificar_SeExcluyeConMotivo()
    {
        var valido = CrearEnvioListo();
        var admitido = CrearEnvioListo(recepcionar: false);
        var sinClasificar = CrearEnvioListo(clasificar: false);

        var resultado = await Ejecutar(Comando(new[] { valido, admitido, sinClasificar }));

        Assert.Single(resultado.Paradas);
        Assert.Contains(resultado.Excluidos, e => e.EnvioId == admitido.Id && e.Motivo.Contains("Admitido"));
        Assert.Contains(resultado.Excluidos, e => e.EnvioId == sinClasificar.Id && e.Motivo.Contains("sin clasificar"));
        Assert.Equal(EstadoEnvio.Admitido, admitido.EstadoActual);
    }

    [Fact]
    public async Task EnvioYaAsignadoAOtraRuta_SeExcluyeIndicandoQueYaFueAsignado()
    {
        var a = CrearEnvioListo();
        var b = CrearEnvioListo();
        await Ejecutar(Comando(new[] { a }));

        var resultado = await Ejecutar(Comando(new[] { a, b }));

        Assert.Single(resultado.Paradas);
        var excluido = Assert.Single(resultado.Excluidos);
        Assert.Equal(a.Id, excluido.EnvioId);
        Assert.Contains("ya fue asignado", excluido.Motivo);
        Assert.Equal(2, _rutas.Rutas.Count);
    }

    [Fact]
    public async Task SegundaOperacionSobreElMismoEnvio_SinOtrosEnvios_SeRechaza()
    {
        var a = CrearEnvioListo();
        await Ejecutar(Comando(new[] { a }));

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(Comando(new[] { a })));

        Assert.Contains("ya fue asignado", ex.Message);
        Assert.Single(_rutas.Rutas);
    }

    [Fact]
    public async Task CapacidadDePeso_ExcluyeLosEnviosQueNoEntran()
    {
        var a = CrearEnvioListo(peso: 60m, codigo: "TRK-A");
        var b = CrearEnvioListo(peso: 60m, codigo: "TRK-B");
        var c = CrearEnvioListo(peso: 30m, codigo: "TRK-C");

        var resultado = await Ejecutar(Comando(new[] { a, b, c }));

        Assert.Equal(new[] { "TRK-A", "TRK-C" }, resultado.Paradas.Select(p => p.CodigoSeguimiento));
        var excluido = Assert.Single(resultado.Excluidos);
        Assert.Equal(b.Id, excluido.EnvioId);
        Assert.Contains("capacidad de peso", excluido.Motivo);
        Assert.Equal(EstadoEnvio.EnDeposito, b.EstadoActual);
        Assert.Equal(90m, resultado.PesoTotal);
    }

    [Fact]
    public async Task PesoExactamenteIgualALaCapacidad_EsValido()
    {
        var a = CrearEnvioListo(peso: 100m);

        var resultado = await Ejecutar(Comando(new[] { a }));

        Assert.Single(resultado.Paradas);
    }

    [Fact]
    public async Task CapacidadDeVolumen_ExcluyeLosEnviosQueNoEntran()
    {
        // Cada bulto de los envíos de prueba ocupa 1000 de volumen
        var chico = new Vehiculo(Guid.NewGuid(), _tenant.OperadorId, "SAB0001", "Moto", 100m, 1500m);
        _vehiculos.Vehiculos.Add(chico);
        var a = CrearEnvioListo(codigo: "TRK-A");
        var b = CrearEnvioListo(codigo: "TRK-B");

        var resultado = await Ejecutar(new ArmarHojaDeRutaCommand(_repartidor.Id, chico.Id, Viernes, new[] { a.Id, b.Id }));

        Assert.Single(resultado.Paradas);
        Assert.Contains("capacidad de volumen", Assert.Single(resultado.Excluidos).Motivo);
    }

    [Fact]
    public async Task CantidadMaximaDeParadas_ExcluyeElExcedente()
    {
        _envios.ReglaOperativa = CrearRegla(maxParadas: 2);
        var a = CrearEnvioListo(codigo: "TRK-A");
        var b = CrearEnvioListo(codigo: "TRK-B");
        var c = CrearEnvioListo(codigo: "TRK-C");

        var resultado = await Ejecutar(Comando(new[] { a, b, c }));

        Assert.Equal(2, resultado.Paradas.Count);
        var excluido = Assert.Single(resultado.Excluidos);
        Assert.Equal(c.Id, excluido.EnvioId);
        Assert.Contains("cantidad máxima de paradas", excluido.Motivo);
    }

    [Fact]
    public async Task SegundoViaje_CadaRutaSeValidaContraLaCapacidadPorSeparado()
    {
        var a = CrearEnvioListo(peso: 60m);
        var b = CrearEnvioListo(peso: 60m);

        var primero = await Ejecutar(Comando(new[] { a, b }));
        var segundo = await Ejecutar(Comando(new[] { b }));

        Assert.Single(primero.Paradas);
        Assert.Single(primero.Excluidos);
        Assert.Single(segundo.Paradas);
        Assert.NotEqual(primero.HojaDeRutaId, segundo.HojaDeRutaId);
        Assert.Equal(2, _rutas.Rutas.Count);
        Assert.Equal(EstadoEnvio.AsignadoARuta, b.EstadoActual);
    }

    [Fact]
    public async Task FranjaCompatible_ConservaLaFranjaEnLaParada()
    {
        var franja = CrearFranja(DayOfWeek.Friday);
        var a = CrearEnvioListo(franja: franja.Id);

        await Ejecutar(Comando(new[] { a }));

        Assert.Equal(franja.Id, _rutas.Rutas.Single().Paradas.Single().FranjaHorariaId);
    }

    [Fact]
    public async Task FranjaDeOtroDiaDeLaSemana_SeExcluyeComoIncompatible()
    {
        var lunes = CrearFranja(DayOfWeek.Monday);
        var incompatible = CrearEnvioListo(franja: lunes.Id);
        var valido = CrearEnvioListo();

        var resultado = await Ejecutar(Comando(new[] { incompatible, valido }));

        Assert.Single(resultado.Paradas);
        Assert.Contains("franja comprometida corresponde a Monday", Assert.Single(resultado.Excluidos).Motivo);
    }

    [Fact]
    public async Task FranjaInactivaOInexistente_SeExcluye()
    {
        var inactiva = CrearFranja(activa: false);
        var conInactiva = CrearEnvioListo(franja: inactiva.Id);
        var conInexistente = CrearEnvioListo(franja: Guid.NewGuid());
        var valido = CrearEnvioListo();

        var resultado = await Ejecutar(Comando(new[] { conInactiva, conInexistente, valido }));

        Assert.Single(resultado.Paradas);
        Assert.Equal(2, resultado.Excluidos.Count);
        Assert.All(resultado.Excluidos, e => Assert.Contains("franja horaria comprometida", e.Motivo));
    }

    [Fact]
    public async Task TodosLosEnviosExcluidos_LanzaDomainValidationExceptionSinPersistir()
    {
        var sinClasificar = CrearEnvioListo(clasificar: false);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(Comando(new[] { sinClasificar })));

        Assert.Contains("Ningún envío pudo incluirse", ex.Message);
        AssertNadaPersistido();
    }

    [Fact]
    public async Task ComandoNulo_LanzaArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CrearHandler().Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task ListaDeEnviosNulaOVacia_LanzaDomainValidationException()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Ejecutar(new ArmarHojaDeRutaCommand(_repartidor.Id, _vehiculo.Id, Viernes, null!)));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Ejecutar(new ArmarHojaDeRutaCommand(_repartidor.Id, _vehiculo.Id, Viernes, new List<Guid>())));
        AssertNadaPersistido();
    }

    [Fact]
    public async Task ListaConIdentificadorVacio_LanzaDomainValidationException()
    {
        var a = CrearEnvioListo();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Ejecutar(new ArmarHojaDeRutaCommand(_repartidor.Id, _vehiculo.Id, Viernes, new[] { a.Id, Guid.Empty })));
        Assert.Equal(EstadoEnvio.EnDeposito, a.EstadoActual);
    }

    [Fact]
    public async Task RepartidorOVehiculoVacios_LanzaDomainValidationException()
    {
        var a = CrearEnvioListo();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Ejecutar(new ArmarHojaDeRutaCommand(Guid.Empty, _vehiculo.Id, Viernes, new[] { a.Id })));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Ejecutar(new ArmarHojaDeRutaCommand(_repartidor.Id, Guid.Empty, Viernes, new[] { a.Id })));
    }

    [Fact]
    public async Task FechaEnElPasado_LanzaDomainValidationException()
    {
        var a = CrearEnvioListo();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Ejecutar(Comando(new[] { a }, fecha: new DateOnly(2026, 10, 10))));
        AssertNadaPersistido();
    }

    [Fact]
    public async Task RepartidorInexistente_LanzaNotFoundException()
    {
        var a = CrearEnvioListo();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Ejecutar(new ArmarHojaDeRutaCommand(Guid.NewGuid(), _vehiculo.Id, Viernes, new[] { a.Id })));
    }

    [Fact]
    public async Task VehiculoInexistente_LanzaNotFoundException()
    {
        var a = CrearEnvioListo();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Ejecutar(new ArmarHojaDeRutaCommand(_repartidor.Id, Guid.NewGuid(), Viernes, new[] { a.Id })));
    }

    [Fact]
    public async Task RepartidorDeOtroOperador_LanzaUnauthorizedAccess()
    {
        var ajeno = new Repartidor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "2.222.222-2", "099222222");
        _repartidores.Repartidores.Add(ajeno);
        var a = CrearEnvioListo();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Ejecutar(new ArmarHojaDeRutaCommand(ajeno.Id, _vehiculo.Id, Viernes, new[] { a.Id })));
        AssertNadaPersistido();
    }

    [Fact]
    public async Task VehiculoDeOtroOperador_LanzaUnauthorizedAccess()
    {
        var ajeno = new Vehiculo(Guid.NewGuid(), Guid.NewGuid(), "AJE0001", "Camión", 500m, 50000m);
        _vehiculos.Vehiculos.Add(ajeno);
        var a = CrearEnvioListo();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Ejecutar(new ArmarHojaDeRutaCommand(_repartidor.Id, ajeno.Id, Viernes, new[] { a.Id })));
    }

    [Fact]
    public async Task RepartidorNoDisponible_LanzaDomainValidationException()
    {
        _repartidor.CambiarEstado("Licencia");
        var a = CrearEnvioListo();

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(Comando(new[] { a })));
        AssertNadaPersistido();
    }

    [Fact]
    public async Task VehiculoInactivo_LanzaDomainValidationException()
    {
        _vehiculo.Desactivar();
        var a = CrearEnvioListo();

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(Comando(new[] { a })));
    }

    [Fact]
    public async Task SinReglaOperativaVigente_LanzaDomainValidationException()
    {
        _envios.ReglaOperativa = null;
        var a = CrearEnvioListo();

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(Comando(new[] { a })));
    }

    [Fact]
    public async Task UsuarioNoIdentificado_LanzaUnauthorizedAccess()
    {
        _tenant.UsuarioId = null;
        var a = CrearEnvioListo();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Ejecutar(Comando(new[] { a })));
    }
}
