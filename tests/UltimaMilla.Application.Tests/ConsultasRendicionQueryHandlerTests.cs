using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Deposito;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class ConsultasRendicionQueryHandlerTests
{
    private static readonly DateOnly Viernes = new(2026, 10, 16);

    private readonly FakeRutaRepository _rutas = new();
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeVehiculoRepository _vehiculos = new();
    private readonly FakeRepartidorRepository _repartidores = new();
    private readonly FakeMotivoNoEntregaRepository _motivos = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();
    private int _secuencia;

    private ListarRutasPendientesRendicionQueryHandler CrearListarHandler() =>
        new(_rutas, _envios, _vehiculos, _repartidores, _tenant);

    private ObtenerRutaParaRendicionQueryHandler CrearDetalleHandler() =>
        new(_rutas, _envios, _vehiculos, _repartidores, _motivos, _tenant);

    private ReglaOperativa Regla(int maxIntentos) =>
        new(Guid.NewGuid(), _tenant.OperadorId, 1, _clock.UtcNow.AddDays(-5), maxIntentos, TimeSpan.FromHours(24),
            "Firma", "Devolución", TimeSpan.FromHours(48), TimeSpan.FromHours(24));

    private Envio CrearEnvio(int bultos = 1)
    {
        _secuencia++;
        var codigo = $"TRK-QRY{_secuencia:0000}";
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", codigo, Guid.NewGuid(),
            null, null, "18 de Julio 1234", "Destinatario Test", _tenant.OperadorId);

        var codigos = new List<string>();
        for (var i = 1; i <= bultos; i++)
        {
            envio.AgregarBulto($"{codigo}-B{i:00}", 1.5m, 10m, 10m, 10m);
            codigos.Add($"{codigo}-B{i:00}");
        }

        envio.RecepcionarEnDeposito(codigos, null, null, Guid.NewGuid());
        envio.ClasificarBultos(codigos.Select(c => new ClasificacionBulto(c, PrioridadBulto.Normal)).ToList(), Guid.NewGuid());
        _envios.Envios.Add(envio);
        return envio;
    }

    private (HojaDeRuta Ruta, Envio Entregado, Envio SinIntento, Envio Reprogramado) CrearRutaConEnvios(string estadoRuta = "Despachada")
    {
        var entregado = CrearEnvio(bultos: 1);
        var sinIntento = CrearEnvio(bultos: 2);
        var reprogramado = CrearEnvio(bultos: 1);
        var todos = new[] { entregado, sinIntento, reprogramado };

        var repartidorId = Guid.NewGuid();
        var vehiculoId = Guid.NewGuid();

        var ruta = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, Viernes, "Borrador");
        ruta.AsignarRecursos(repartidorId, vehiculoId);

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
        {
            envio.DespacharEnRuta(ruta.Id, Guid.NewGuid());
        }

        var paradaId = (Envio e) => ruta.Paradas.Single(p => p.EnvioId == e.Id).Id;

        // Entregado exitoso
        entregado.RegistrarEntregaExitosa(paradaId(entregado), Regla(3), "Receptor", "4.123.456-7", "firma-base64", null, null, null, DateTime.UtcNow);
        ruta.RegistrarEntregaExitosaEnParada(paradaId(entregado));

        // Reprogramado por intento fallido
        var motivoCalle = new MotivoNoEntrega(Guid.NewGuid(), _tenant.OperadorId, "AUSENTE", "Cliente ausente");
        reprogramado.RegistrarIntentoFallido(paradaId(reprogramado), motivoCalle, Regla(3), "No atendió", null, null, null, DateTime.UtcNow);
        ruta.RegistrarIntentoFallidoEnParada(paradaId(reprogramado));

        // sinIntento queda en EnTransito

        if (estadoRuta == "Finalizada")
        {
            ruta.Finalizar();
        }

        _rutas.Rutas.Add(ruta);
        return (ruta, entregado, sinIntento, reprogramado);
    }

    [Fact]
    public async Task ListarRutasPendientesRendicion_FiltraPorEstadosCorrectosYCalculaBultos()
    {
        var (rutaDespachada, _, _, _) = CrearRutaConEnvios("Despachada");
        var (rutaFinalizada, _, _, _) = CrearRutaConEnvios("Finalizada");

        // Otra ruta en borrador que NO debe incluirse
        var rutaBorrador = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, Viernes, "Borrador");
        _rutas.Rutas.Add(rutaBorrador);

        var handler = CrearListarHandler();
        var resultado = await handler.Handle(new ListarRutasPendientesRendicionQuery(), CancellationToken.None);

        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, r => r.HojaDeRutaId == rutaDespachada.Id);
        Assert.Contains(resultado, r => r.HojaDeRutaId == rutaFinalizada.Id);
        Assert.DoesNotContain(resultado, r => r.HojaDeRutaId == rutaBorrador.Id);

        var item = resultado.First(r => r.HojaDeRutaId == rutaDespachada.Id);
        Assert.Equal(3, item.CantidadParadas);
        Assert.Equal(4, item.TotalBultos); // 1 + 2 + 1
        Assert.Equal(1, item.BultosEntregadosEstimados);
        Assert.Equal(3, item.BultosRetornoEstimados); // 4 - 1
    }

    [Fact]
    public async Task ListarRutasPendientesRendicion_SinRutas_DevuelveVacio()
    {
        var handler = CrearListarHandler();
        var resultado = await handler.Handle(new ListarRutasPendientesRendicionQuery(), CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarRutasPendientesRendicion_RequestNulo_LanzaArgumentNullException()
    {
        var handler = CrearListarHandler();
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task ObtenerRutaParaRendicion_DevuelveDetalleCompletoYDetectaEnviosSinIntento()
    {
        var (ruta, entregado, sinIntento, reprogramado) = CrearRutaConEnvios("Despachada");

        var motivoInterno = new MotivoNoEntrega(Guid.NewGuid(), _tenant.OperadorId, "RETORNO_SIN_INTENTO", "No se visitó por avería", esInterno: true);
        var motivoCalle = new MotivoNoEntrega(Guid.NewGuid(), _tenant.OperadorId, "AUSENTE", "Cliente ausente", esInterno: false);
        _motivos.Motivos.Add(motivoInterno);
        _motivos.Motivos.Add(motivoCalle);

        var handler = CrearDetalleHandler();
        var resultado = await handler.Handle(new ObtenerRutaParaRendicionQuery(ruta.Id), CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal(ruta.Id, resultado.HojaDeRutaId);
        Assert.Equal("Despachada", resultado.Estado);
        Assert.True(resultado.TieneEnviosSinIntento);
        Assert.Equal(3, resultado.TotalParadas);
        Assert.Equal(4, resultado.TotalBultos);
        Assert.Equal(1, resultado.BultosEntregadosCount);
        Assert.Equal(3, resultado.BultosEsperadosRetornoCount);

        // Motivos internos: solo el interno activo
        Assert.Single(resultado.MotivosInternosDisponibles);
        Assert.Equal("RETORNO_SIN_INTENTO", resultado.MotivosInternosDisponibles[0].Codigo);

        // Detalle de envíos
        var envioSinIntentoDto = resultado.Envios.First(e => e.EnvioId == sinIntento.Id);
        Assert.True(envioSinIntentoDto.EsRetornoSinIntento);
        Assert.True(envioSinIntentoDto.RequiereRetorno);
        Assert.False(envioSinIntentoDto.EsEntregado);
        Assert.Equal(2, envioSinIntentoDto.Bultos.Count);

        var envioEntregadoDto = resultado.Envios.First(e => e.EnvioId == entregado.Id);
        Assert.True(envioEntregadoDto.EsEntregado);
        Assert.False(envioEntregadoDto.RequiereRetorno);
        Assert.False(envioEntregadoDto.EsRetornoSinIntento);
    }

    [Fact]
    public async Task ObtenerRutaParaRendicion_IdVacio_LanzaDomainValidationException()
    {
        var handler = CrearDetalleHandler();
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(new ObtenerRutaParaRendicionQuery(Guid.Empty), CancellationToken.None));
    }

    [Fact]
    public async Task ObtenerRutaParaRendicion_NoExistente_LanzaNotFoundException()
    {
        var handler = CrearDetalleHandler();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ObtenerRutaParaRendicionQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task ObtenerRutaParaRendicion_OtroOperador_LanzaUnauthorizedAccessException()
    {
        var (ruta, _, _, _) = CrearRutaConEnvios("Despachada");
        _tenant.OperadorId = Guid.NewGuid();

        var handler = CrearDetalleHandler();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new ObtenerRutaParaRendicionQuery(ruta.Id), CancellationToken.None));
    }

    [Fact]
    public async Task ObtenerRutaParaRendicion_EstadoInvalidoBorrador_LanzaDomainValidationException()
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, Viernes, "Borrador");
        _rutas.Rutas.Add(ruta);

        var handler = CrearDetalleHandler();
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(new ObtenerRutaParaRendicionQuery(ruta.Id), CancellationToken.None));
    }

    [Fact]
    public async Task ObtenerRutaParaRendicion_RequestNulo_LanzaArgumentNullException()
    {
        var handler = CrearDetalleHandler();
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
