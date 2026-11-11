using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Mobile.ObtenerRutaActivaRepartidor;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Tests;

public class ObtenerRutaActivaRepartidorQueryHandlerTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 20);

    private readonly FakeRutaRepository _rutas = new();
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeRepartidorRepository _repartidores = new();
    private readonly FakeVehiculoRepository _vehiculos = new();
    private readonly FakeDestinatarioRepository _destinatarios = new();
    private readonly FakeDireccionRepository _direcciones = new();
    private readonly FakeFranjaHorariaRepository _franjas = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly Repartidor _repartidor;
    private readonly Vehiculo _vehiculo;
    private int _secuencia;

    public ObtenerRutaActivaRepartidorQueryHandlerTests()
    {
        _repartidor = new Repartidor(Guid.NewGuid(), _tenant.UsuarioId!.Value, _tenant.OperadorId, "1.111.111-1", "099111111");
        _repartidores.Repartidores.Add(_repartidor);

        _vehiculo = new Vehiculo(Guid.NewGuid(), _tenant.OperadorId, "SBA-4321", "Furgón Master", 1200m, 12m);
        _vehiculos.Vehiculos.Add(_vehiculo);

        _envios.Motivos.Add(new MotivoNoEntrega(Guid.NewGuid(), _tenant.OperadorId, "DEST_AUSENTE", "Destinatario ausente"));
    }

    private ObtenerRutaActivaRepartidorQueryHandler CrearHandler() =>
        new(_rutas, _envios, _repartidores, _vehiculos, _destinatarios, _direcciones, _franjas, _tenant);

    private (Envio Envio, Destinatario Destinatario, Direccion Direccion) CrearEnvio(int bultos = 1)
    {
        _secuencia++;
        var codigo = $"ENV-2026-{107 + _secuencia:D5}";
        var destinatario = new Destinatario(Guid.NewGuid(), $"Destinatario {_secuencia}", "4.123.456-7", $"dest{_secuencia}@mail.com", "099111222");
        var direccion = new Direccion(Guid.NewGuid(), "Av. 18 de Julio", $"{1400 + _secuencia}", "Montevideo", null, -34.9058, -56.1885, "Timbre 201");
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), destinatario.Id, "Estandar", codigo,
            direccion.Id, null, null, direccion.ToString(), destinatario.Nombre, _tenant.OperadorId);

        var codigos = new List<string>();
        for (var i = 1; i <= bultos; i++)
        {
            var codBulto = $"BLT-00{_secuencia}-{(char)('A' + i - 1)}";
            envio.AgregarBulto(codBulto, 1.5m, 20m, 20m, 15m);
            codigos.Add(codBulto);
        }

        envio.RecepcionarEnDeposito(codigos, null, null, Guid.NewGuid());
        envio.ClasificarBultos(codigos.Select(c => new ClasificacionBulto(c, PrioridadBulto.Normal)).ToList(), Guid.NewGuid());

        _envios.Envios.Add(envio);
        _destinatarios.Destinatarios.Add(destinatario);
        _direcciones.Direcciones.Add(direccion);
        return (envio, destinatario, direccion);
    }

    private HojaDeRuta CrearRutaDespachada(params Envio[] envios)
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, Hoy);
        ruta.AsignarRecursos(_repartidor.Id, _vehiculo.Id);
        var orden = 1;
        foreach (var envio in envios)
        {
            ruta.AgregarParada(envio.Id, orden++, envio.DireccionEntregaId!.Value, envio.FranjaHorariaComprometidaId);
            ruta.RegistrarAsignacion(envio.Id, Guid.NewGuid());
            envio.AsignarRuta(ruta.Id);
        }

        ruta.Liberar();
        ruta.RegistrarCargaEscaneada(envios.SelectMany(e => e.Bultos).Select(b => b.Id));
        ruta.Despachar();

        _rutas.Rutas.Add(ruta);
        return ruta;
    }

    [Fact]
    public void Constructor_DependenciasNulas_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ObtenerRutaActivaRepartidorQueryHandler(
            null!, _envios, _repartidores, _vehiculos, _destinatarios, _direcciones, _franjas, _tenant));
        Assert.Throws<ArgumentNullException>(() => new ObtenerRutaActivaRepartidorQueryHandler(
            _rutas, null!, _repartidores, _vehiculos, _destinatarios, _direcciones, _franjas, _tenant));
        Assert.Throws<ArgumentNullException>(() => new ObtenerRutaActivaRepartidorQueryHandler(
            _rutas, _envios, null!, _vehiculos, _destinatarios, _direcciones, _franjas, _tenant));
        Assert.Throws<ArgumentNullException>(() => new ObtenerRutaActivaRepartidorQueryHandler(
            _rutas, _envios, _repartidores, null!, _destinatarios, _direcciones, _franjas, _tenant));
        Assert.Throws<ArgumentNullException>(() => new ObtenerRutaActivaRepartidorQueryHandler(
            _rutas, _envios, _repartidores, _vehiculos, null!, _direcciones, _franjas, _tenant));
        Assert.Throws<ArgumentNullException>(() => new ObtenerRutaActivaRepartidorQueryHandler(
            _rutas, _envios, _repartidores, _vehiculos, _destinatarios, null!, _franjas, _tenant));
        Assert.Throws<ArgumentNullException>(() => new ObtenerRutaActivaRepartidorQueryHandler(
            _rutas, _envios, _repartidores, _vehiculos, _destinatarios, _direcciones, null!, _tenant));
        Assert.Throws<ArgumentNullException>(() => new ObtenerRutaActivaRepartidorQueryHandler(
            _rutas, _envios, _repartidores, _vehiculos, _destinatarios, _direcciones, _franjas, null!));
    }

    [Fact]
    public async Task Handle_RequestNulo_LanzaArgumentNullException()
    {
        var handler = CrearHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle((ObtenerRutaActivaRepartidorQuery)null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle((ObtenerRutasActivasRepartidorQuery)null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UsuarioNoAutenticado_LanzaUnauthorizedAccessException()
    {
        _tenant.UsuarioId = null;
        var handler = CrearHandler();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.Handle(new ObtenerRutaActivaRepartidorQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UsuarioIdVacio_LanzaUnauthorizedAccessException()
    {
        _tenant.UsuarioId = Guid.Empty;
        var handler = CrearHandler();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.Handle(new ObtenerRutaActivaRepartidorQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UsuarioSinPerfilDeRepartidor_LanzaNotFoundException()
    {
        _tenant.UsuarioId = Guid.NewGuid();
        var handler = CrearHandler();

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new ObtenerRutaActivaRepartidorQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RepartidorDeOtroOperador_LanzaUnauthorizedAccessException()
    {
        var otroOperadorId = Guid.NewGuid();
        var otroUsuarioId = Guid.NewGuid();
        _repartidores.Repartidores.Add(new Repartidor(Guid.NewGuid(), otroUsuarioId, otroOperadorId, "2.222.222-2", "099222222"));
        _tenant.UsuarioId = otroUsuarioId;

        var handler = CrearHandler();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.Handle(new ObtenerRutaActivaRepartidorQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RepartidorSinRutaActiva_LanzaNotFoundException()
    {
        var handler = CrearHandler();

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new ObtenerRutaActivaRepartidorQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RepartidorConRutaDespachada_RetornaRutaActivaConParadasBultosYDestinatarios()
    {
        var (e1, d1, dir1) = CrearEnvio(bultos: 2);
        var (e2, d2, _) = CrearEnvio(bultos: 1);
        var ruta = CrearRutaDespachada(e1, e2);

        var handler = CrearHandler();
        var resultado = await handler.Handle(new ObtenerRutaActivaRepartidorQuery(), CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal(ruta.Id, resultado.HojaDeRutaId);
        Assert.Equal(_repartidor.Id, resultado.RepartidorId);
        Assert.Equal("Despachada", resultado.Estado);
        Assert.Equal("SBA-4321", resultado.PatenteVehiculo);
        Assert.Equal(2, resultado.Paradas.Count);
        Assert.Equal(new[] { 1, 2 }, resultado.Paradas.Select(p => p.Orden));
        Assert.Equal(e1.CodigoSeguimientoPublico, resultado.Paradas[0].CodigoSeguimiento);
        Assert.Equal(d1.Nombre, resultado.Paradas[0].Destinatario.Nombre);
        Assert.Equal(dir1.Latitud, resultado.Paradas[0].Latitud);
        Assert.Equal(dir1.Longitud, resultado.Paradas[0].Longitud);
        Assert.Equal(2, resultado.Paradas[0].ResumenBultos.CantidadBultos);
        Assert.Equal(d2.Nombre, resultado.Paradas[1].Destinatario.Nombre);
        Assert.Single(resultado.MotivosNoEntrega);
    }
}
