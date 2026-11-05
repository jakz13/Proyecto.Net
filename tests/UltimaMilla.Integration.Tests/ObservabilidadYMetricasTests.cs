using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using UltimaMilla.Api.Observabilidad;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Infrastructure.Observabilidad;
using UltimaMilla.Infrastructure.Services;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Pruebas unitarias y de integración de UC-44 (§8.4):
/// - CorrelationIdMiddleware (sin encabezado, válido, inválido >64 o no imprimible, devolución en respuesta).
/// - DistributedCacheService (aciertos y fallos incrementan las métricas correspondientes).
/// - Endpoint GET /metrics (responde 200 OK y expone http_server_request_duration_seconds y métricas de negocio).
/// </summary>
public class ObservabilidadYMetricasTests
{
    // =========================================================================
    // 1. PRUEBAS UNITARIAS DE CorrelationIdMiddleware (§8.4)
    // =========================================================================

    [Fact]
    public async Task CorrelationIdMiddleware_SinEncabezado_GeneraUnoNuevoYLoDevuelveEnLaRespuesta()
    {
        var contexto = new DefaultHttpContext();
        string? correlacionDuranteEjecucion = null;

        var middleware = new CorrelationIdMiddleware(_ =>
        {
            correlacionDuranteEjecucion = ContextoCorrelacion.CorrelationIdActual;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(contexto);

        var devuelto = contexto.Response.Headers[CorrelationIdMiddleware.NombreCabecera].ToString();
        Assert.False(string.IsNullOrWhiteSpace(devuelto));
        Assert.True(Guid.TryParse(devuelto, out _));
        Assert.Equal(devuelto, correlacionDuranteEjecucion);
        Assert.Equal(devuelto, contexto.Items["CorrelationId"]);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_ConEncabezadoValido_LoConservaYLoDevuelveEnLaRespuesta()
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Headers[CorrelationIdMiddleware.NombreCabecera] = "demo-001";
        string? correlacionEnActividad = null;

        using var actividad = new Activity("SolicitudPrueba").Start();
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            correlacionEnActividad = Activity.Current?.GetTagItem(ContextoCorrelacion.EtiquetaActividad)?.ToString();
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(contexto);

        var devuelto = contexto.Response.Headers[CorrelationIdMiddleware.NombreCabecera].ToString();
        Assert.Equal("demo-001", devuelto);
        Assert.Equal("demo-001", correlacionEnActividad);
        Assert.Equal("demo-001", contexto.Items["CorrelationId"]);
    }

    [Theory]
    [InlineData("demasiado-largo-123456789012345678901234567890123456789012345678901234567890")]
    [InlineData("inyeccion-log\r\nERROR falso")]
    [InlineData("caracter-control-\u0007-invalido")]
    [InlineData("   ")]
    public async Task CorrelationIdMiddleware_ConEncabezadoInvalido_LoReemplazaYDevuelveNuevoEnRespuesta(string encabezadoInvalido)
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Headers[CorrelationIdMiddleware.NombreCabecera] = encabezadoInvalido;

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(contexto);

        var devuelto = contexto.Response.Headers[CorrelationIdMiddleware.NombreCabecera].ToString();
        Assert.False(string.IsNullOrWhiteSpace(devuelto));
        Assert.NotEqual(encabezadoInvalido, devuelto);
        Assert.True(Guid.TryParse(devuelto, out _));
    }

    [Fact]
    public async Task CorrelationIdMiddleware_ConParametrosNulos_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CorrelationIdMiddleware(null!));

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        await Assert.ThrowsAsync<ArgumentNullException>(() => middleware.InvokeAsync(null!));
    }

    [Theory]
    [InlineData("cliente@dominio.com.uy", "c***@dominio.com.uy")]
    [InlineData("41234567", "41***67")]
    [InlineData("123", "***")]
    [InlineData("", "***")]
    public void EnriquecedorObservabilidad_EnmascaraDatosPersonalesSegunRegla05(string entrada, string esperado)
    {
        Assert.Equal(esperado, EnriquecedorObservabilidad.EnmascararPii(entrada));
    }

    // =========================================================================
    // 2. PRUEBAS UNITARIAS DE DistributedCacheService (§8.4)
    // =========================================================================

    [Fact]
    public async Task DistributedCacheService_AciertoIncrementaAciertos_FalloIncrementaFallos()
    {
        var memoria = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        using var metricas = new MetricasNegocio();
        var servicioCache = new DistributedCacheService(memoria, metricas);

        // 1. Consulta de clave inexistente -> fallo (miss)
        var inexistente = await servicioCache.GetAsync<DatoPruebaCache>("clave-no-existe");
        Assert.Null(inexistente);
        Assert.Equal(0, metricas.TotalAciertosCache);
        Assert.Equal(1, metricas.TotalFallosCache);

        // 2. Guardar dato y volver a consultar -> acierto (hit)
        await servicioCache.SetAsync("clave-existe", new DatoPruebaCache("ENV-001"), TimeSpan.FromMinutes(5));
        var encontrado = await servicioCache.GetAsync<DatoPruebaCache>("clave-existe");

        Assert.NotNull(encontrado);
        Assert.Equal("ENV-001", encontrado!.Codigo);
        Assert.Equal(1, metricas.TotalAciertosCache);
        Assert.Equal(1, metricas.TotalFallosCache);

        // 3. Eliminar y volver a consultar -> segundo fallo (miss)
        await servicioCache.RemoveAsync("clave-existe");
        var eliminado = await servicioCache.GetAsync<DatoPruebaCache>("clave-existe");
        Assert.Null(eliminado);
        Assert.Equal(1, metricas.TotalAciertosCache);
        Assert.Equal(2, metricas.TotalFallosCache);
    }

    private sealed record DatoPruebaCache(string Codigo);

    // =========================================================================
    // 3. PRUEBA DE INTEGRACIÓN DE GET /metrics (§8.4)
    // =========================================================================

    [Fact]
    public async Task GetMetrics_Responde200YContieneDuracionDeSolicitudesYMetricasDeNegocio()
    {
        await using var fabrica = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Database:SkipMigrationsOnStartup"] = "true",
                        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=ultimamilla_metrics_test;Username=postgres;Password=postgres"
                    });
                });
            });

        using var cliente = fabrica.CreateClient();

        // Generar una solicitud previa con X-Correlation-Id para que el histograma de ASP.NET Core registre muestras
        using var solicitudLive = new HttpRequestMessage(HttpMethod.Get, "/live");
        solicitudLive.Headers.Add("X-Correlation-Id", "demo-001");
        using var respuestaLive = await cliente.SendAsync(solicitudLive);

        Assert.Equal(HttpStatusCode.OK, respuestaLive.StatusCode);
        Assert.True(respuestaLive.Headers.TryGetValues("X-Correlation-Id", out var valoresCorrelacion));
        Assert.Contains("demo-001", valoresCorrelacion!);

        // Consultar el endpoint de Prometheus
        using var respuestaMetricas = await cliente.GetAsync("/metrics");
        var cuerpoMetricas = await respuestaMetricas.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, respuestaMetricas.StatusCode);
        Assert.Contains("http_server_request_duration_seconds", cuerpoMetricas);
        Assert.Contains("um_cache_aciertos_total", cuerpoMetricas);
        Assert.Contains("um_cache_fallos_total", cuerpoMetricas);
        Assert.Contains("um_cola_mensajes_pendientes", cuerpoMetricas);
        Assert.Contains("um_sync_eventos_pendientes", cuerpoMetricas);
        Assert.Contains("um_avisos_comercio_fallidos", cuerpoMetricas);
        Assert.Contains("um_avisos_comercio_intentos_total", cuerpoMetricas);
        Assert.Contains("um_sync_eventos_recibidos_total", cuerpoMetricas);
    }
}

