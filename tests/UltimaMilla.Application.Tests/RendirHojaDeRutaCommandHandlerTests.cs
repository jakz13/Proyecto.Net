using UltimaMilla.Application.Commands.Deposito.RendirHojaDeRuta;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class RendirHojaDeRutaCommandHandlerTests
{
    private static readonly DateOnly Viernes = new(2026, 10, 16);

    private readonly FakeRutaRepository _rutas = new();
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeRendicionRepository _rendiciones = new();
    private readonly FakeOutboxStorage _outbox = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();
    private readonly MotivoNoEntrega _motivoInterno;
    private readonly MotivoNoEntrega _motivoCalle;
    private int _secuencia;

    public RendirHojaDeRutaCommandHandlerTests()
    {
        _motivoInterno = new MotivoNoEntrega(Guid.NewGuid(), _tenant.OperadorId, "RETORNO_SIN_INTENTO", "Retorno sin intento", esInterno: true);
        _motivoCalle = new MotivoNoEntrega(Guid.NewGuid(), _tenant.OperadorId, "AUSENTE", "Cliente ausente");
        _envios.Motivos.Add(_motivoInterno);
        _envios.Motivos.Add(_motivoCalle);
    }

    private RendirHojaDeRutaCommandHandler CrearHandler() =>
        new(_rutas, _envios, _rendiciones, _outbox, _uow, _tenant, _clock);

    private ReglaOperativa Regla(int maxIntentos) =>
        new(Guid.NewGuid(), _tenant.OperadorId, 1, _clock.UtcNow.AddDays(-5), maxIntentos, TimeSpan.FromHours(24),
            "Firma", "Devolución", TimeSpan.FromHours(48), TimeSpan.FromHours(24));

    private Envio CrearEnvioEnDeposito(int bultos = 1)
    {
        _secuencia++;
        var codigo = $"TRK-REND{_secuencia:0000}";
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", codigo, Guid.NewGuid(),
            null, null, "Rivera 10", "Ana", _tenant.OperadorId);

        var codigos = new List<string>();
        for (var i = 1; i <= bultos; i++)
        {
            envio.AgregarBulto($"{codigo}-B{i:00}", 2m, 10m, 10m, 10m);
            codigos.Add($"{codigo}-B{i:00}");
        }

        envio.RecepcionarEnDeposito(codigos, null, null, Guid.NewGuid());
        envio.ClasificarBultos(codigos.Select(c => new ClasificacionBulto(c, PrioridadBulto.Normal)).ToList(), Guid.NewGuid());
        _envios.Envios.Add(envio);
        return envio;
    }

    /// <summary>Ruta despachada con un envío en cada situación posible al regresar a depósito.</summary>
    private (HojaDeRuta Ruta, Envio Entregado, Envio SinIntento, Envio Reprogramado, Envio EnDevolucion) CrearRutaDespachada()
    {
        var entregado = CrearEnvioEnDeposito();
        var sinIntento = CrearEnvioEnDeposito(bultos: 2);
        var reprogramado = CrearEnvioEnDeposito();
        var enDevolucion = CrearEnvioEnDeposito();
        var todos = new[] { entregado, sinIntento, reprogramado, enDevolucion };

        var ruta = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, Viernes);
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        var orden = 1;
        foreach (var envio in todos)
        {
            ruta.AgregarParada(envio.Id, orden++, envio.DireccionEntregaId!.Value);
            ruta.RegistrarAsignacion(envio.Id, Guid.NewGuid());
            envio.AsignarRuta(ruta.Id);
        }

        ruta.Liberar();
        ruta.RegistrarCargaEscaneada(todos.SelectMany(e => e.Bultos).Select(b => b.Id));
        ruta.Despachar();
        foreach (var envio in todos)
            envio.DespacharEnRuta(ruta.Id, Guid.NewGuid());

        var paradaId = (Envio e) => ruta.Paradas.Single(p => p.EnvioId == e.Id).Id;

        entregado.RegistrarEntregaExitosa(paradaId(entregado), Regla(3), "Juan", "1.234.567-8", "firma", null, null, null, DateTime.UtcNow);
        ruta.RegistrarEntregaExitosaEnParada(paradaId(entregado));

        reprogramado.RegistrarIntentoFallido(paradaId(reprogramado), _motivoCalle, Regla(3), "Nadie atendió", null, null, null, DateTime.UtcNow);
        ruta.RegistrarIntentoFallidoEnParada(paradaId(reprogramado));

        enDevolucion.RegistrarIntentoFallido(paradaId(enDevolucion), _motivoCalle, Regla(1), "Dirección inexistente", null, null, null, DateTime.UtcNow);
        ruta.RegistrarIntentoFallidoEnParada(paradaId(enDevolucion));

        _rutas.Rutas.Add(ruta);
        return (ruta, entregado, sinIntento, reprogramado, enDevolucion);
    }

    private static List<string> Codigos(params Envio[] envios) =>
        envios.SelectMany(e => e.Bultos).Select(b => b.Identificacion).ToList();

    private Task<RendicionResultDto> Rendir(HojaDeRuta ruta, IEnumerable<string> devueltos, string? motivo = "RETORNO_SIN_INTENTO", string? obs = null) =>
        CrearHandler().Handle(new RendirHojaDeRutaCommand(ruta.Id, devueltos.ToList(), motivo, obs), CancellationToken.None);

    [Fact]
    public async Task Rendicion_Completa_ResuelveCadaEnvioSegunSuSituacion()
    {
        var (ruta, entregado, sinIntento, reprogramado, enDevolucion) = CrearRutaDespachada();

        var resultado = await Rendir(ruta, Codigos(sinIntento, reprogramado, enDevolucion));

        Assert.Equal("Rendida", resultado.EstadoRuta);
        Assert.Equal(1, resultado.BultosEntregados);
        Assert.Equal(4, resultado.BultosDevueltos);
        Assert.Equal(0, resultado.BultosPendientes);
        Assert.False(resultado.Idempotente);

        Assert.Equal(EstadoEnvio.Entregado, entregado.EstadoActual);
        Assert.Equal(EstadoEnvio.EnDeposito, sinIntento.EstadoActual);
        Assert.Equal(EstadoEnvio.EnDeposito, reprogramado.EstadoActual);
        Assert.Equal(EstadoEnvio.EnDevolucion, enDevolucion.EstadoActual);

        Assert.Equal(2, resultado.EnviosReintegradosADeposito.Count);
        Assert.Contains(enDevolucion.CodigoSeguimientoPublico, resultado.EnviosEnDevolucion);
        Assert.Empty(resultado.EnviosPendientes);
        Assert.Equal(1, _uow.Confirmaciones);
        Assert.Single(_rendiciones.Rendiciones);
    }

    [Fact]
    public async Task Rendicion_EnvioSinIntento_QuedaNoEntregadoConMotivoInternoYVuelveADeposito()
    {
        var (ruta, _, sinIntento, reprogramado, enDevolucion) = CrearRutaDespachada();

        await Rendir(ruta, Codigos(sinIntento, reprogramado, enDevolucion), obs: "Vehículo averiado");

        var eventos = sinIntento.Eventos.Select(e => e.TipoEvento).ToList();
        Assert.Contains("RetornoSinIntento", eventos);
        Assert.Equal("RetornoADeposito", eventos.Last());
        Assert.Contains(sinIntento.Eventos, e => e.EstadoNuevo == EstadoEnvio.NoEntregado && e.Payload!.Contains("RETORNO_SIN_INTENTO"));
        Assert.Null(sinIntento.HojaDeRutaId);
        Assert.Empty(sinIntento.IntentosEntrega);
        Assert.All(sinIntento.Bultos, b => Assert.Equal("Recibido", b.EstadoBulto));
    }

    [Fact]
    public async Task Rendicion_CierraLaRutaYHabilitaAlRepartidor()
    {
        var (ruta, _, sinIntento, reprogramado, enDevolucion) = CrearRutaDespachada();
        var repartidorId = ruta.RepartidorId!.Value;
        Assert.True(await _rutas.ExisteRutaSinRendirAsync(repartidorId, Guid.NewGuid()));

        await Rendir(ruta, Codigos(sinIntento, reprogramado, enDevolucion));

        Assert.Equal("Rendida", ruta.Estado);
        Assert.NotNull(ruta.FechaRendicion);
        Assert.All(ruta.Asignaciones, a => Assert.False(a.Vigente));
        Assert.False(await _rutas.ExisteRutaSinRendirAsync(repartidorId, Guid.NewGuid()));
        Assert.Contains(ruta.Paradas, p => p.Estado == "NoVisitada");
    }

    [Fact]
    public async Task Rendicion_EnvioReintegrado_PuedeAsignarseAOtraRuta()
    {
        var (ruta, _, sinIntento, reprogramado, enDevolucion) = CrearRutaDespachada();

        await Rendir(ruta, Codigos(sinIntento, reprogramado, enDevolucion));

        Assert.Null(sinIntento.ObtenerMotivoNoAsignable());
        Assert.Null(reprogramado.ObtenerMotivoNoAsignable());
    }

    [Fact]
    public async Task Rendicion_BultosNoPresentados_QuedanPendientesYElEnvioNoCambia()
    {
        var (ruta, _, sinIntento, reprogramado, enDevolucion) = CrearRutaDespachada();
        // Del envío de dos bultos solo se presenta uno
        var presentados = Codigos(reprogramado, enDevolucion).Append(sinIntento.Bultos.First().Identificacion);

        var resultado = await Rendir(ruta, presentados);

        Assert.Equal(1, resultado.BultosPendientes);
        Assert.Equal(EstadoEnvio.EnTransito, sinIntento.EstadoActual);
        Assert.Contains(sinIntento.CodigoSeguimientoPublico, resultado.EnviosPendientes);
        Assert.Equal(EstadoEnvio.EnDeposito, reprogramado.EstadoActual);
        Assert.Equal("Rendida", ruta.Estado);
    }

    [Fact]
    public async Task Rendicion_ReprogramadoSinPresentar_ConservaSuEstado()
    {
        var (ruta, _, sinIntento, reprogramado, enDevolucion) = CrearRutaDespachada();

        var resultado = await Rendir(ruta, Codigos(sinIntento, enDevolucion));

        Assert.Equal(EstadoEnvio.Reprogramado, reprogramado.EstadoActual);
        Assert.Equal(1, resultado.BultosPendientes);
        Assert.Contains(reprogramado.CodigoSeguimientoPublico, resultado.EnviosPendientes);
    }

    [Fact]
    public async Task Rendicion_CodigosAjenosALaRuta_SeInformanComoSobrantes()
    {
        var (ruta, _, sinIntento, reprogramado, enDevolucion) = CrearRutaDespachada();

        var resultado = await Rendir(ruta, Codigos(sinIntento, reprogramado, enDevolucion).Append("  bulto-ajeno "));

        Assert.Equal("BULTO-AJENO", Assert.Single(resultado.CodigosSobrantes));
        Assert.Contains("diferencias", resultado.Mensaje);
    }

    [Fact]
    public async Task Rendicion_CodigosEnMinusculasYDuplicados_SeNormalizan()
    {
        var (ruta, _, sinIntento, reprogramado, enDevolucion) = CrearRutaDespachada();
        var codigos = Codigos(sinIntento, reprogramado, enDevolucion);
        var variantes = codigos.Select(c => c.ToLowerInvariant()).Concat(codigos);

        var resultado = await Rendir(ruta, variantes);

        Assert.Equal(0, resultado.BultosPendientes);
        Assert.Empty(resultado.CodigosSobrantes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NO_EXISTE")]
    [InlineData("AUSENTE")]
    public async Task Rendicion_ConEnviosSinIntento_RequiereUnMotivoInternoValido(string? motivo)
    {
        var (ruta, _, sinIntento, reprogramado, enDevolucion) = CrearRutaDespachada();

        await Assert.ThrowsAsync<DomainValidationException>(() => Rendir(ruta, Codigos(sinIntento, reprogramado, enDevolucion), motivo));

        Assert.Equal("Despachada", ruta.Estado);
        Assert.Equal(EstadoEnvio.EnTransito, sinIntento.EstadoActual);
        Assert.Equal(EstadoEnvio.Reprogramado, reprogramado.EstadoActual);
        Assert.Empty(_rendiciones.Rendiciones);
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task Rendicion_SinEnviosSinIntento_NoExigeMotivo()
    {
        var (ruta, _, _, reprogramado, enDevolucion) = CrearRutaDespachada();

        var resultado = await Rendir(ruta, Codigos(reprogramado, enDevolucion), motivo: null);

        Assert.Equal("Rendida", resultado.EstadoRuta);
    }

    [Fact]
    public async Task Rendicion_Repetida_EsIdempotenteYNoDuplicaRegistros()
    {
        var (ruta, _, sinIntento, reprogramado, enDevolucion) = CrearRutaDespachada();
        var codigos = Codigos(sinIntento, reprogramado, enDevolucion);
        await Rendir(ruta, codigos);
        var eventos = sinIntento.Eventos.Count;

        var segunda = await Rendir(ruta, codigos);

        Assert.True(segunda.Idempotente);
        Assert.Equal(1, segunda.BultosEntregados);
        Assert.Single(_rendiciones.Rendiciones);
        Assert.Equal(eventos, sinIntento.Eventos.Count);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task Rendicion_RutaNoDespachada_LanzaDomainValidationException()
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, Viernes);
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        ruta.AgregarParada(Guid.NewGuid(), 1, Guid.NewGuid());
        ruta.Liberar();
        _rutas.Rutas.Add(ruta);

        await Assert.ThrowsAsync<DomainValidationException>(() => Rendir(ruta, new[] { "X" }));
        Assert.Equal("Liberada", ruta.Estado);
    }

    [Fact]
    public async Task Rendicion_GeneraEventosDeIntegracion()
    {
        var (ruta, _, sinIntento, reprogramado, enDevolucion) = CrearRutaDespachada();

        await Rendir(ruta, Codigos(sinIntento, reprogramado, enDevolucion));

        Assert.Equal(2, _outbox.Mensajes.Count(m => m.TipoEvento == "EnvioReintegradoADepositoIntegrationEvent"));
        Assert.Single(_outbox.Mensajes, m => m.TipoEvento == "RutaRendidaIntegrationEvent");
    }

    [Fact]
    public async Task Rendicion_RutaInexistenteIdVacioOComandoNulo_LanzaExcepcionesTipadas()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CrearHandler().Handle(new RendirHojaDeRutaCommand(Guid.NewGuid(), new List<string>()), default));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(new RendirHojaDeRutaCommand(Guid.Empty, new List<string>()), default));
        await Assert.ThrowsAsync<ArgumentNullException>(() => CrearHandler().Handle(null!, default));
    }

    [Fact]
    public async Task Rendicion_RutaDeOtroOperador_LanzaUnauthorizedAccess()
    {
        var (ruta, _, sinIntento, reprogramado, enDevolucion) = CrearRutaDespachada();
        _tenant.OperadorId = Guid.NewGuid();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Rendir(ruta, Codigos(sinIntento, reprogramado, enDevolucion)));
        Assert.Equal("Despachada", ruta.Estado);
    }

    [Fact]
    public async Task Rendicion_UsuarioNoIdentificado_LanzaUnauthorizedAccess()
    {
        var (ruta, _, _, _, _) = CrearRutaDespachada();
        _tenant.UsuarioId = null;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Rendir(ruta, new[] { "X" }));
    }

    [Fact]
    public async Task MotivosInternos_NoSeOfrecenEnLaAppDelRepartidor()
    {
        var visibles = await _envios.ListarMotivosNoEntregaActivosAsync(_tenant.OperadorId);

        Assert.DoesNotContain(visibles, m => m.EsInterno);
        Assert.Single(visibles);
    }
}
