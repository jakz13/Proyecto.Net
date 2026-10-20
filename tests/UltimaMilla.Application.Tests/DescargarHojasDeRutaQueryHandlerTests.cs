using UltimaMilla.Application.Queries.Mobile.DescargarHojasDeRuta;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Tests;

public class DescargarHojasDeRutaQueryHandlerTests
{
    // El reloj fijo es el jueves 15/10/2026
    private static readonly DateOnly Hoy = new(2026, 10, 15);

    private readonly FakeRutaRepository _rutas = new();
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeRepartidorRepository _repartidores = new();
    private readonly FakeDestinatarioRepository _destinatarios = new();
    private readonly FakeDireccionRepository _direcciones = new();
    private readonly FakeFranjaHorariaRepository _franjas = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();
    private readonly Repartidor _repartidor;
    private int _secuencia;

    public DescargarHojasDeRutaQueryHandlerTests()
    {
        _repartidor = new Repartidor(Guid.NewGuid(), _tenant.UsuarioId!.Value, _tenant.OperadorId, "1.111.111-1", "099111111");
        _repartidores.Repartidores.Add(_repartidor);
        _envios.ReglaOperativa = new ReglaOperativa(Guid.NewGuid(), _tenant.OperadorId, 1, _clock.UtcNow.AddDays(-5), 3,
            TimeSpan.FromHours(24), "Firma digital y foto", "Devolución", TimeSpan.FromHours(48), TimeSpan.FromHours(24));
        _envios.Motivos.Add(new MotivoNoEntrega(Guid.NewGuid(), _tenant.OperadorId, "AUSENTE", "Destinatario ausente"));
    }

    private DescargarHojasDeRutaQueryHandler CrearHandler() =>
        new(_rutas, _envios, _repartidores, _destinatarios, _direcciones, _franjas, _tenant, _clock);

    private Task<DescargaHojasDeRutaDto> Ejecutar(DateOnly? fecha = null) =>
        CrearHandler().Handle(new DescargarHojasDeRutaQuery(fecha), CancellationToken.None);

    private (Envio Envio, Destinatario Destinatario, Direccion Direccion) CrearEnvio(Guid? operadorId = null, int bultos = 1, Guid? franja = null)
    {
        _secuencia++;
        var codigo = $"TRK-DESC{_secuencia:0000}";
        var destinatario = new Destinatario(Guid.NewGuid(), $"Destinatario {_secuencia}", "1.234.567-8", $"d{_secuencia}@correo.com", "099000000");
        var direccion = new Direccion(Guid.NewGuid(), "Rivera", $"{_secuencia}", "Montevideo", null, -34.9, -56.1, "Timbre 2");
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), destinatario.Id, "Estandar", codigo,
            direccion.Id, franja, null, direccion.ToString(), destinatario.Nombre, operadorId ?? _tenant.OperadorId);

        var codigos = new List<string>();
        for (var i = 1; i <= bultos; i++)
        {
            envio.AgregarBulto($"{codigo}-B{i:00}", 2m, 10m, 10m, 10m);
            codigos.Add($"{codigo}-B{i:00}");
        }

        envio.RecepcionarEnDeposito(codigos, null, null, Guid.NewGuid());
        envio.ClasificarBultos(codigos.Select(c => new ClasificacionBulto(c, PrioridadBulto.Alta)).ToList(), Guid.NewGuid());

        _envios.Envios.Add(envio);
        _destinatarios.Destinatarios.Add(destinatario);
        _direcciones.Direcciones.Add(direccion);
        return (envio, destinatario, direccion);
    }

    private HojaDeRuta CrearRuta(Guid? repartidorId = null, DateOnly? fecha = null, string estado = "Liberada", params Envio[] envios)
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, fecha ?? Hoy);
        ruta.AsignarRecursos(repartidorId ?? _repartidor.Id, Guid.NewGuid());
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

    [Fact]
    public async Task Descarga_DevuelveLaRutaConParadasOrdenadasYTodosLosDatosDeLaParada()
    {
        var franja = new FranjaHoraria(Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Thursday, new TimeOnly(9, 0), new TimeOnly(12, 30));
        _franjas.Franjas.Add(franja);
        var (e1, d1, dir1) = CrearEnvio(bultos: 2, franja: franja.Id);
        var (e2, _, _) = CrearEnvio();
        var ruta = CrearRuta(envios: new[] { e1, e2 });

        var resultado = await Ejecutar();

        var rutaDto = Assert.Single(resultado.Rutas);
        Assert.Equal(ruta.Id, rutaDto.HojaDeRutaId);
        Assert.Equal("Liberada", rutaDto.Estado);
        Assert.Equal(ruta.SelloModificacion, rutaDto.SelloModificacion);
        Assert.Equal(new[] { 1, 2 }, rutaDto.Paradas.Select(p => p.Orden));

        var parada = rutaDto.Paradas[0];
        Assert.Equal(e1.Id, parada.EnvioId);
        Assert.Equal(e1.CodigoSeguimientoPublico, parada.CodigoSeguimiento);
        Assert.Equal("09:00-12:30", parada.VentanaHoraria);
        Assert.Equal(d1.Nombre, parada.Destinatario.Nombre);
        Assert.Equal("099000000", parada.Destinatario.Telefono);
        Assert.Equal(dir1.ToString(), parada.Direccion.Texto);
        Assert.Equal(-34.9, parada.Direccion.Latitud);
        Assert.Equal("Timbre 2", parada.Direccion.Referencia);
        Assert.Equal(2, parada.Bultos.Count);
        Assert.All(parada.Bultos, b => Assert.Equal("Alta", b.Prioridad));
        Assert.Equal(e1.SelloModificacion, parada.SelloEnvio);
        Assert.Null(rutaDto.Paradas[1].VentanaHoraria);
    }

    [Fact]
    public async Task Descarga_IncluyeLaConfiguracionParaOperarSinConexion()
    {
        var (e, _, _) = CrearEnvio();
        CrearRuta(envios: e);

        var resultado = await Ejecutar();

        Assert.Equal("Firma digital y foto", resultado.Configuracion.TipoPruebaEntregaExigida);
        Assert.Equal(3, resultado.Configuracion.MaxIntentosEntrega);
        Assert.Equal("AUSENTE", Assert.Single(resultado.Configuracion.MotivosNoEntrega).Codigo);
    }

    [Fact]
    public async Task Descarga_SinFecha_UsaElDiaActual()
    {
        var (e, _, _) = CrearEnvio();
        CrearRuta(fecha: Hoy, envios: e);
        var (e2, _, _) = CrearEnvio();
        CrearRuta(fecha: Hoy.AddDays(1), envios: e2);

        var hoy = await Ejecutar();
        var manana = await Ejecutar(Hoy.AddDays(1));

        Assert.Equal(Hoy, hoy.Fecha);
        Assert.Single(hoy.Rutas);
        Assert.Equal(Hoy.AddDays(1), manana.Fecha);
        Assert.Single(manana.Rutas);
    }

    [Theory]
    [InlineData("Liberada")]
    [InlineData("EnCarga")]
    [InlineData("Despachada")]
    public async Task Descarga_IncluyeLasRutasEnEstadosDescargables(string estado)
    {
        var (e, _, _) = CrearEnvio();
        CrearRuta(estado: estado, envios: e);

        var resultado = await Ejecutar();

        Assert.Equal(estado, Assert.Single(resultado.Rutas).Estado);
    }

    [Fact]
    public async Task Descarga_NoIncluyeRutasFinalizadasNiBorradores()
    {
        var (e, _, _) = CrearEnvio();
        var ruta = CrearRuta(estado: "Despachada", envios: e);
        ruta.RegistrarEntregaExitosaEnParada(ruta.Paradas.Single().Id);
        Assert.Equal("Finalizada", ruta.Estado);

        var borrador = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, Hoy);
        borrador.AsignarRecursos(_repartidor.Id, Guid.NewGuid());
        _rutas.Rutas.Add(borrador);

        var resultado = await Ejecutar();

        Assert.Empty(resultado.Rutas);
    }

    [Fact]
    public async Task Descarga_SinRutas_DevuelveListaVaciaConMensaje()
    {
        var resultado = await Ejecutar();

        Assert.Empty(resultado.Rutas);
        Assert.Contains("No hay hojas de ruta", resultado.Mensaje);
        Assert.Equal("AUSENTE", Assert.Single(resultado.Configuracion.MotivosNoEntrega).Codigo);
    }

    [Fact]
    public async Task Descarga_NoIncluyeRutasDeOtroRepartidor()
    {
        var (e, _, _) = CrearEnvio();
        CrearRuta(repartidorId: Guid.NewGuid(), envios: e);

        var resultado = await Ejecutar();

        Assert.Empty(resultado.Rutas);
    }

    [Fact]
    public async Task Descarga_OmiteParadasCanceladasYBultosFaltantesEnRecepcion()
    {
        var (completo, _, _) = CrearEnvio();
        var (cancelado, _, _) = CrearEnvio();
        var ruta = CrearRuta(envios: new[] { completo, cancelado });
        ruta.DesasignarEnvioFaltante(cancelado.Id);

        var resultado = await Ejecutar();

        var parada = Assert.Single(resultado.Rutas.Single().Paradas);
        Assert.Equal(completo.Id, parada.EnvioId);
    }

    [Fact]
    public async Task Descarga_EnvioDeOtroOperador_NoSeIncluye()
    {
        var (propio, _, _) = CrearEnvio();
        var (ajeno, _, _) = CrearEnvio(operadorId: Guid.NewGuid());
        CrearRuta(envios: new[] { propio, ajeno });

        var resultado = await Ejecutar();

        Assert.Equal(propio.Id, Assert.Single(resultado.Rutas.Single().Paradas).EnvioId);
    }

    [Fact]
    public async Task Descarga_SinDestinatarioNiDireccionPersistidos_UsaLaCopiaDesnormalizada()
    {
        var (e, d, dir) = CrearEnvio();
        CrearRuta(envios: e);
        _destinatarios.Destinatarios.Remove(d);
        _direcciones.Direcciones.Remove(dir);

        var parada = (await Ejecutar()).Rutas.Single().Paradas.Single();

        Assert.Equal(e.NombreDestinatario, parada.Destinatario.Nombre);
        Assert.Equal(e.DireccionDestino, parada.Direccion.Texto);
        Assert.Null(parada.Direccion.Latitud);
    }

    [Fact]
    public async Task Descarga_SinReglaOperativaNiMotivos_DevuelveConfiguracionVacia()
    {
        _envios.ReglaOperativa = null;
        _envios.Motivos.Clear();

        var resultado = await Ejecutar();

        Assert.Null(resultado.Configuracion.TipoPruebaEntregaExigida);
        Assert.Null(resultado.Configuracion.MaxIntentosEntrega);
        Assert.Empty(resultado.Configuracion.MotivosNoEntrega);
    }

    [Fact]
    public async Task Descarga_UsuarioQueNoEsRepartidor_LanzaUnauthorizedAccess()
    {
        _repartidores.Repartidores.Clear();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Ejecutar());
    }

    [Fact]
    public async Task Descarga_UsuarioNoIdentificado_LanzaUnauthorizedAccess()
    {
        _tenant.UsuarioId = null;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Ejecutar());
    }

    [Fact]
    public async Task Descarga_RepartidorDeOtroOperador_LanzaUnauthorizedAccess()
    {
        _tenant.OperadorId = Guid.NewGuid();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Ejecutar());
    }

    [Fact]
    public async Task Descarga_ConsultaNula_LanzaArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CrearHandler().Handle(null!, CancellationToken.None));
    }
}
