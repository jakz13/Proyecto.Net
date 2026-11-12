using UltimaMilla.Application.Commands.Mensajeria;
using UltimaMilla.Application.Events;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Seguimiento;

namespace UltimaMilla.Application.Tests;

/// <summary>
/// Pruebas unitarias exhaustivas de correlación distribuida, métricas de negocio y arquitectura limpia (Regla 08).
/// </summary>
public class ObservabilidadCorrelacionYMuestrasTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizarOGenerar_SinValorOEnBlanco_GeneraGuidValido(string? entrada)
    {
        var resultado = ContextoCorrelacion.NormalizarOGenerar(entrada);

        Assert.False(string.IsNullOrWhiteSpace(resultado));
        Assert.True(ContextoCorrelacion.EsValido(resultado));
        Assert.True(Guid.TryParse(resultado, out _));
    }

    [Theory]
    [InlineData("demo-001")]
    [InlineData("corr-abc-123_XYZ")]
    [InlineData("f47ac10b-58cc-4372-a567-0e02b2c3d479")]
    public void NormalizarOGenerar_ConValorValido_ConservaElValor(string entrada)
    {
        var resultado = ContextoCorrelacion.NormalizarOGenerar(entrada);

        Assert.Equal(entrada, resultado);
    }

    [Fact]
    public void NormalizarOGenerar_ConMasDe64Caracteres_DescartaYGeneraNuevo()
    {
        var exactamente64 = new string('a', 64);
        var masDe64 = new string('a', 65);

        Assert.Equal(exactamente64, ContextoCorrelacion.NormalizarOGenerar(exactamente64));

        var reemplazado = ContextoCorrelacion.NormalizarOGenerar(masDe64);
        Assert.NotEqual(masDe64, reemplazado);
        Assert.True(Guid.TryParse(reemplazado, out _));
    }

    [Theory]
    [InlineData("demo-001\r\nFAKE_LOG")]
    [InlineData("demo-001\ninyectado")]
    [InlineData("demo\0nulo")]
    [InlineData("demo\ttab")]
    [InlineData("con espacio interno")]
    public void NormalizarOGenerar_ConCaracteresNoImprimiblesOInyeccion_DescartaYGeneraNuevo(string entradaMaliciosa)
    {
        var resultado = ContextoCorrelacion.NormalizarOGenerar(entradaMaliciosa);

        Assert.NotEqual(entradaMaliciosa, resultado);
        Assert.True(ContextoCorrelacion.EsValido(resultado));
        Assert.True(Guid.TryParse(resultado, out _));
    }

    [Fact]
    public void IniciarAlcance_PropagaCorrelationIdAEventosDeIntegracionYRestauraAlDisponer()
    {
        using (ContextoCorrelacion.IniciarAlcance("demo-001", "11111111-1111-1111-1111-111111111111"))
        {
            Assert.Equal("demo-001", ContextoCorrelacion.ObtenerCorrelationIdActual());
            Assert.Equal("11111111-1111-1111-1111-111111111111", ContextoCorrelacion.TenantIdActual);

            var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-OBS-01", operadorId: Guid.NewGuid());
            var outbox = EventosIntegracion.DeEnvio(EventosIntegracion.Tipos.EnvioAdmitido, envio);

            Assert.Equal("demo-001", PayloadMensaje.CorrelationIdDe(outbox.Payload));
        }

        Assert.Null(ContextoCorrelacion.CorrelationIdActual);
        Assert.Null(ContextoCorrelacion.TenantIdActual);
    }

    [Fact]
    public async Task AvisoComercioEntregador_RegistraMetricasDeIntentosExitososYFallidos()
    {
        var avisoRepo = new FakeAvisoRepoLocal();
        var suscripcion = new SuscripcionAviso(Guid.NewGuid(), Guid.NewGuid(), "https://comercio.uy/webhook", "*", "secreto-firma-123456");
        await avisoRepo.AddSuscripcionAsync(suscripcion);

        var aviso1 = new AvisoComercio(Guid.NewGuid(), suscripcion.Id, Guid.NewGuid(), "{\"estado\":\"Entregado\"}", "sha256=abc");
        var aviso2 = new AvisoComercio(Guid.NewGuid(), suscripcion.Id, Guid.NewGuid(), "{\"estado\":\"NoEntregado\"}", "sha256=def");

        var metricas = new FakeMetricasNegocio();
        var webhookOk = new FakeWebhookLocal { Respuesta = new ResultadoWebhook(true, 200, "HTTP 200 OK") };
        var entregadorOk = new AvisoComercioEntregador(avisoRepo, webhookOk, new FakeUnitOfWork(), new FakeClock(), metricas);

        await entregadorOk.EntregarAsync(new[] { aviso1 });
        Assert.Equal(1, metricas.IntentosAvisoOk);
        Assert.Equal(0, metricas.IntentosAvisoError);

        var webhookError = new FakeWebhookLocal { Respuesta = new ResultadoWebhook(false, 500, "HTTP 500 Error") };
        var entregadorError = new AvisoComercioEntregador(avisoRepo, webhookError, new FakeUnitOfWork(), new FakeClock(), metricas);

        await entregadorError.EntregarAsync(new[] { aviso2 });
        Assert.Equal(1, metricas.IntentosAvisoOk);
        Assert.Equal(1, metricas.IntentosAvisoError);
    }

    [Fact]
    public void ArquitecturaLimpia_ApplicationYDomainNoDependenDeOpenTelemetryNiSerilog()
    {
        var ensambladosDomain = typeof(Envio).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();
        Assert.DoesNotContain(ensambladosDomain, n => n.StartsWith("UltimaMilla.", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(ensambladosDomain, n => n.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(ensambladosDomain, n => n.Contains("Serilog", StringComparison.OrdinalIgnoreCase));

        var ensambladosApplication = typeof(IMetricasNegocio).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();
        Assert.DoesNotContain(ensambladosApplication, n => n.Equals("UltimaMilla.Infrastructure", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(ensambladosApplication, n => n.Equals("UltimaMilla.Api", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(ensambladosApplication, n => n.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(ensambladosApplication, n => n.Contains("Serilog", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeMetricasNegocio : IMetricasNegocio
    {
        public int AciertosCache { get; private set; }
        public int FallosCache { get; private set; }
        public int IntentosAvisoOk { get; private set; }
        public int IntentosAvisoError { get; private set; }
        public int EventosSincronizacion { get; private set; }

        public void RegistrarAciertoCache() => AciertosCache++;
        public void RegistrarFalloCache() => FallosCache++;
        public void RegistrarIntentoAviso(bool exito)
        {
            if (exito) IntentosAvisoOk++;
            else IntentosAvisoError++;
        }
        public void RegistrarEventoSincronizacion(int cantidad = 1) => EventosSincronizacion += cantidad;
    }

    private sealed class FakeAvisoRepoLocal : IAvisoComercioRepository
    {
        private readonly List<SuscripcionAviso> _suscripciones = new();
        private readonly List<AvisoComercio> _avisos = new();

        public Task<List<SuscripcionAviso>> ListarSuscripcionesActivasAsync(Guid comercioOperadorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_suscripciones.Where(s => s.ComercioOperadorId == comercioOperadorId && s.Activa).ToList());

        public Task<SuscripcionAviso?> GetSuscripcionAsync(Guid suscripcionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_suscripciones.FirstOrDefault(s => s.Id == suscripcionId));

        public Task<List<SuscripcionAviso>> ListarSuscripcionesAsync(Guid? comercioOperadorId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(_suscripciones.ToList());

        public Task<SuscripcionAviso?> GetSuscripcionParaEdicionAsync(Guid suscripcionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_suscripciones.FirstOrDefault(s => s.Id == suscripcionId));

        public Task AddSuscripcionAsync(SuscripcionAviso suscripcion, CancellationToken cancellationToken = default)
        {
            _suscripciones.Add(suscripcion);
            return Task.CompletedTask;
        }

        public Task<List<AvisoComercio>> ListarHistorialAvisosAsync(Guid? comercioOperadorId = null, string? estado = null, int limite = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult(_avisos.Take(limite).ToList());

        public Task<AvisoComercio?> GetAvisoByIdAsync(Guid avisoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_avisos.FirstOrDefault(a => a.Id == avisoId));

        public Task AddAsync(AvisoComercio aviso, CancellationToken cancellationToken = default)
        {
            _avisos.Add(aviso);
            return Task.CompletedTask;
        }

        public Task<List<AvisoComercio>> ListarPendientesDeEntregaAsync(DateTime ahoraUtc, int maximo, CancellationToken cancellationToken = default) =>
            Task.FromResult(_avisos.Take(maximo).ToList());
    }

    private sealed class FakeWebhookLocal : IWebhookSender
    {
        public ResultadoWebhook Respuesta { get; set; } = new(true, 200, "HTTP 200 OK");

        public Task<ResultadoWebhook> EnviarAsync(string urlWebhook, string payload, string firma, Guid avisoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Respuesta);
    }
}
