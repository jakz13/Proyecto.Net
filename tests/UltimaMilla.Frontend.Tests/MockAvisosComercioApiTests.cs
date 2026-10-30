using UltimaMilla.Frontend.Contracts.Modelos.Avisos;
using UltimaMilla.Frontend.Infrastructure.Excepciones;
using UltimaMilla.Frontend.Infrastructure.Mocks;
using Xunit;

namespace UltimaMilla.Frontend.Tests;

/// <summary>
/// Pruebas unitarias para MockAvisosComercioApi (UC-29 Configurar avisos y UC-30 Historial y Reintentos).
/// </summary>
public class MockAvisosComercioApiTests
{
    [Fact]
    public async Task ObtenerConfiguracionAsync_RetornaConfiguracionInicialValidaConCatalogo()
    {
        // Arrange
        var api = new MockAvisosComercioApi();

        // Act
        var config = await api.ObtenerConfiguracionAsync();

        // Assert
        Assert.NotNull(config);
        Assert.True(config.EstaConfigurado);
        Assert.False(string.IsNullOrWhiteSpace(config.UrlWebhook));
        Assert.NotEmpty(config.EventosSuscritos);
        Assert.NotEmpty(config.CatalogoEventos);
        Assert.NotEmpty(config.HistorialReciente);
    }

    [Fact]
    public async Task GuardarConfiguracionAsync_ConDatosValidos_ActualizaConfiguracionYGeneraClave()
    {
        // Arrange
        var api = new MockAvisosComercioApi();
        var nuevaConfig = new GuardarConfiguracionAvisosDto
        {
            UrlWebhook = "https://tienda.com/api/webhooks/pedidos",
            Eventos = new[] { "Entregado", "NoEntregado" },
            ClaveFirma = null,
            Activa = true
        };

        // Act
        var resultado = await api.GuardarConfiguracionAsync(nuevaConfig);
        var configActualizada = await api.ObtenerConfiguracionAsync();

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("https://tienda.com/api/webhooks/pedidos", resultado.UrlWebhook);
        Assert.False(string.IsNullOrWhiteSpace(resultado.ClaveFirma));
        Assert.Equal(2, resultado.EventosSuscritos.Count);

        Assert.Equal("https://tienda.com/api/webhooks/pedidos", configActualizada.UrlWebhook);
        Assert.Equal(2, configActualizada.EventosSuscritos.Count);
        Assert.Contains("Entregado", configActualizada.EventosSuscritos);
        Assert.Contains("NoEntregado", configActualizada.EventosSuscritos);
    }

    [Fact]
    public async Task GuardarConfiguracionAsync_ConClaveManual_PreservaClaveIndicada()
    {
        // Arrange
        var api = new MockAvisosComercioApi();
        var claveManual = "secreto-comercio-abc-123";
        var dto = new GuardarConfiguracionAvisosDto
        {
            UrlWebhook = "https://tienda.com/api/webhook",
            Eventos = new[] { "EnvioAdmitido" },
            ClaveFirma = claveManual,
            Activa = true
        };

        // Act
        var resultado = await api.GuardarConfiguracionAsync(dto);

        // Assert
        Assert.Equal(claveManual, resultado.ClaveFirma);
        var configActual = await api.ObtenerConfiguracionAsync();
        Assert.Equal(claveManual, configActual.ClaveFirma);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-url")]
    [InlineData("ftp://servidor.com")]
    public async Task GuardarConfiguracionAsync_UrlInvalida_LanzaApiExcepcion(string urlInvalida)
    {
        // Arrange
        var api = new MockAvisosComercioApi();
        var dto = new GuardarConfiguracionAvisosDto
        {
            UrlWebhook = urlInvalida,
            Eventos = new[] { "Entregado" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ApiExcepcion>(() => api.GuardarConfiguracionAsync(dto));
    }

    [Fact]
    public async Task GuardarConfiguracionAsync_SinEventos_LanzaApiExcepcion()
    {
        // Arrange
        var api = new MockAvisosComercioApi();
        var dto = new GuardarConfiguracionAvisosDto
        {
            UrlWebhook = "https://tienda.com/api/webhook",
            Eventos = Array.Empty<string>()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ApiExcepcion>(() => api.GuardarConfiguracionAsync(dto));
    }

    [Fact]
    public async Task EnviarPruebaWebhookAsync_ConUrlValida_GeneraPruebaExitosaYFirma()
    {
        // Arrange
        var api = new MockAvisosComercioApi();

        // Act
        var resultado = await api.EnviarPruebaWebhookAsync("https://tienda.com/api/webhook");

        // Assert
        Assert.NotNull(resultado);
        Assert.True(resultado.Exitoso);
        Assert.Equal(200, resultado.CodigoHttp);
        Assert.False(string.IsNullOrWhiteSpace(resultado.FirmaGenerada));
        Assert.Contains("ping", resultado.PayloadEnviado.ToLowerInvariant());
    }

    [Fact]
    public async Task EnviarPruebaWebhookAsync_SinUrlOpcional_UsaUrlConfiguradaPreviamente()
    {
        // Arrange
        var api = new MockAvisosComercioApi();

        // Act
        var resultado = await api.EnviarPruebaWebhookAsync();

        // Assert
        Assert.NotNull(resultado);
        Assert.True(resultado.Exitoso);
        Assert.Equal(200, resultado.CodigoHttp);
    }

    [Theory]
    [InlineData("invalida")]
    [InlineData("ftp://servidor.com")]
    [InlineData("javascript:alert(1)")]
    public async Task EnviarPruebaWebhookAsync_UrlInvalida_LanzaApiExcepcion(string urlInvalida)
    {
        // Arrange
        var api = new MockAvisosComercioApi();

        // Act & Assert
        await Assert.ThrowsAsync<ApiExcepcion>(() => api.EnviarPruebaWebhookAsync(urlInvalida));
    }

    [Fact]
    public async Task ConsultarHistorialAvisosAsync_FiltraPorEstadoYTracking()
    {
        // Arrange
        var api = new MockAvisosComercioApi();

        // Act
        var todos = await api.ConsultarHistorialAvisosAsync();
        var fallidos = await api.ConsultarHistorialAvisosAsync(estado: "Fallido");
        var porTracking = await api.ConsultarHistorialAvisosAsync(codigoSeguimiento: "TRK-2026-0001");

        // Assert
        Assert.NotEmpty(todos);
        Assert.All(fallidos, f => Assert.Equal("Fallido", f.Estado));
        Assert.All(porTracking, t => Assert.Contains("TRK-2026-0001", t.CodigoSeguimiento));
    }

    [Fact]
    public async Task ReintentarAvisoAsync_AvisoFallido_CambiaAEntregadoYResponde200()
    {
        // Arrange
        var api = new MockAvisosComercioApi();
        var avisoFallidoId = Guid.Parse("33333333-aaaa-bbbb-cccc-000000000003");

        // Act
        var resultado = await api.ReintentarAvisoAsync(avisoFallidoId);

        // Assert
        Assert.NotNull(resultado);
        Assert.True(resultado.Exitoso);
        Assert.Equal("Entregado", resultado.Estado);
        Assert.Equal(200, resultado.CodigoHttp);

        var historial = await api.ConsultarHistorialAvisosAsync();
        var itemActualizado = Assert.Single(historial, h => h.Id == avisoFallidoId);
        Assert.Equal("Entregado", itemActualizado.Estado);
        Assert.Equal(200, itemActualizado.CodigoRespuestaHttp);
    }

    [Fact]
    public async Task ReintentarAvisoAsync_IdVacio_LanzaApiExcepcion()
    {
        var api = new MockAvisosComercioApi();
        await Assert.ThrowsAsync<ApiExcepcion>(() => api.ReintentarAvisoAsync(Guid.Empty));
    }

    [Fact]
    public async Task ReintentarAvisoAsync_IdInexistente_LanzaApiExcepcion()
    {
        var api = new MockAvisosComercioApi();
        await Assert.ThrowsAsync<ApiExcepcion>(() => api.ReintentarAvisoAsync(Guid.NewGuid()));
    }
}
