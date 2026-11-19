using UltimaMilla.Application.Commands.Comercio.ConfigurarAvisos;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Comercio.ObtenerConfiguracionAvisos;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

/// <summary>
/// Pruebas exhaustivas para Configurar avisos automáticos y webhooks.
/// Valida flujos felices, seguridad, reglas de formato, idoneidad de eventos, persistencia e idempotencia.
/// </summary>
public class ConfigurarAvisosHandlerTests
{
    private readonly FakeSuscripcionAvisoRepository _suscripcionesRepo = new();
    private readonly FakeAvisoComercioConsultaRepository _avisosRepo;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentTenant _tenant = new();

    public ConfigurarAvisosHandlerTests()
    {
        _avisosRepo = new FakeAvisoComercioConsultaRepository(_suscripcionesRepo);
    }

    private ConfigurarAvisosCommandHandler CrearConfigurarHandler() =>
        new(_suscripcionesRepo, _tenant, _unitOfWork);

    private readonly EmisorWebhookDePrueba _emisor = new();

    private ProbarWebhookCommandHandler CrearProbarHandler() =>
        new(_suscripcionesRepo, _tenant, _emisor);

    /// <summary>Emisor falso: registra lo que se envió y responde lo que cada prueba le indique.</summary>
    private sealed class EmisorWebhookDePrueba : IWebhookSender
    {
        public List<(string Url, string Payload, string Firma)> Llamadas { get; } = new();
        public ResultadoWebhook Respuesta { get; set; } = new(true, 200, "HTTP 200 OK");

        public Task<ResultadoWebhook> EnviarAsync(string urlWebhook, string payload, string firma, Guid avisoId, CancellationToken cancellationToken = default)
        {
            Llamadas.Add((urlWebhook, payload, firma));
            return Task.FromResult(Respuesta);
        }
    }

    private ObtenerConfiguracionAvisosQueryHandler CrearObtenerHandler() =>
        new(_suscripcionesRepo, _tenant, _avisosRepo);

    [Fact]
    public async Task ConfigurarAvisos_Exitoso_GeneraClaveFirmaPersisteYConfirmaUnitOfWork()
    {
        // Arrange
        var handler = CrearConfigurarHandler();
        var comando = new ConfigurarAvisosCommand(
            UrlWebhook: "https://comercio.ejemplo.com/api/webhooks",
            Eventos: new[] { "EnvioAdmitido", "EnTransito", "Entregado" },
            ClaveFirma: null,
            Activa: true);

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("https://comercio.ejemplo.com/api/webhooks", resultado.UrlWebhook);
        Assert.Equal(3, resultado.Eventos.Count);
        Assert.False(string.IsNullOrWhiteSpace(resultado.ClaveFirma));
        Assert.True(resultado.ClaveFirma.Length >= 32);
        Assert.True(_unitOfWork.Confirmaciones >= 1);

        // Verificar persistencia en el repositorio
        var suscripcionesGuardadas = await _suscripcionesRepo.ListarPorComercioOperadorAsync(_tenant.ComercioOperadorId!.Value);
        Assert.Equal(3, suscripcionesGuardadas.Count);
        Assert.All(suscripcionesGuardadas, s =>
        {
            Assert.Equal("https://comercio.ejemplo.com/api/webhooks", s.UrlWebhook);
            Assert.Equal(resultado.ClaveFirma, s.ClaveFirma);
            Assert.True(s.Activa);
        });
    }

    [Fact]
    public async Task ConfigurarAvisos_ConClaveFirmaPersonalizada_MantieneLaClaveIndicada()
    {
        // Arrange
        var handler = CrearConfigurarHandler();
        var clavePersonalizada = "mi-clave-super-secreta-2026";
        var comando = new ConfigurarAvisosCommand(
            UrlWebhook: "https://comercio.ejemplo.com/api/notificaciones",
            Eventos: new[] { "Entregado" },
            ClaveFirma: clavePersonalizada,
            Activa: true);

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.Equal(clavePersonalizada, resultado.ClaveFirma);
        var suscripciones = await _suscripcionesRepo.ListarPorComercioOperadorAsync(_tenant.ComercioOperadorId!.Value);
        Assert.Single(suscripciones);
        Assert.Equal(clavePersonalizada, suscripciones[0].ClaveFirma);
    }

    [Fact]
    public async Task ConfigurarAvisos_ReconfiguracionIdempotente_ReemplazaSuscripcionesAnteriores()
    {
        // Arrange
        var handler = CrearConfigurarHandler();
        var comando1 = new ConfigurarAvisosCommand(
            UrlWebhook: "https://v1.comercio.com/webhook",
            Eventos: new[] { "EnvioAdmitido", "EnTransito" },
            ClaveFirma: "clave-v1",
            Activa: true);
        await handler.Handle(comando1, CancellationToken.None);

        var comando2 = new ConfigurarAvisosCommand(
            UrlWebhook: "https://v2.comercio.com/webhook",
            Eventos: new[] { "Entregado" },
            ClaveFirma: "clave-v2",
            Activa: false);

        // Act
        var resultado2 = await handler.Handle(comando2, CancellationToken.None);

        // Assert
        var suscripciones = await _suscripcionesRepo.ListarPorComercioOperadorAsync(_tenant.ComercioOperadorId!.Value);
        Assert.Single(suscripciones);
        Assert.Equal("Entregado", suscripciones[0].TipoEvento);
        Assert.Equal("https://v2.comercio.com/webhook", suscripciones[0].UrlWebhook);
        Assert.False(suscripciones[0].Activa);
        Assert.Single(resultado2.Eventos);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public async Task ConfigurarAvisos_UrlNulaOVacia_LanzaDomainValidationException(string? urlInvalida)
    {
        var handler = CrearConfigurarHandler();
        var comando = new ConfigurarAvisosCommand(
            UrlWebhook: urlInvalida!,
            Eventos: new[] { "Entregado" });

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(comando, CancellationToken.None));
    }

    [Theory]
    [InlineData("ftp://servidor.com/webhook")]
    [InlineData("no-es-una-url")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/datos/webhook")]
    public async Task ConfigurarAvisos_UrlConEsquemaInvalido_LanzaDomainValidationException(string urlInvalida)
    {
        var handler = CrearConfigurarHandler();
        var comando = new ConfigurarAvisosCommand(
            UrlWebhook: urlInvalida,
            Eventos: new[] { "Entregado" });

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(comando, CancellationToken.None));
    }

    [Fact]
    public async Task ConfigurarAvisos_ListaEventosVaciaONula_LanzaDomainValidationException()
    {
        var handler = CrearConfigurarHandler();
        var comandoVacio = new ConfigurarAvisosCommand(
            UrlWebhook: "https://comercio.com/webhook",
            Eventos: Array.Empty<string>());

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(comandoVacio, CancellationToken.None));
    }

    [Theory]
    [InlineData("EventoInexistente")]
    [InlineData("HACK_EVENT")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ConfigurarAvisos_EventoNoPermitido_LanzaDomainValidationException(string eventoInvalido)
    {
        var handler = CrearConfigurarHandler();
        var comando = new ConfigurarAvisosCommand(
            UrlWebhook: "https://comercio.com/webhook",
            Eventos: new[] { "Entregado", eventoInvalido });

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(comando, CancellationToken.None));
    }

    [Fact]
    public async Task ConfigurarAvisos_UsuarioSinComercioOperador_LanzaUnauthorizedAccessException()
    {
        _tenant.ComercioOperadorId = null;
        var handler = CrearConfigurarHandler();
        var comando = new ConfigurarAvisosCommand(
            UrlWebhook: "https://comercio.com/webhook",
            Eventos: new[] { "Entregado" });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(comando, CancellationToken.None));
    }

    [Fact]
    public async Task ProbarWebhook_GeneraDiagnosticoYFirmaHmacValida()
    {
        // Arrange
        var handler = CrearProbarHandler();
        var comando = new ProbarWebhookCommand("https://mi-tienda.com/api/test-webhook");

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.True(resultado.Exitoso);
        Assert.Equal(200, resultado.CodigoHttp);
        Assert.False(string.IsNullOrWhiteSpace(resultado.FirmaGenerada));
        Assert.Contains("ping", resultado.PayloadEnviado.ToLowerInvariant());

        // Hace una llamada real: lo que se muestra es exactamente lo que salió hacia el comercio
        var llamada = Assert.Single(_emisor.Llamadas);
        Assert.Equal("https://mi-tienda.com/api/test-webhook", llamada.Url);
        Assert.Equal(resultado.PayloadEnviado, llamada.Payload);
        Assert.Equal(resultado.FirmaGenerada, llamada.Firma);
    }

    [Fact]
    public async Task ProbarWebhook_ElComercioResponde500_ElPingMuestraElFalloEnVezDeUnExitoInventado()
    {
        // Antes el ping devolvía siempre "HTTP 200 OK" sin enviar nada: un comercio con el receptor caído nunca se enteraba
        _emisor.Respuesta = new ResultadoWebhook(false, 500, "HTTP 500 Internal Server Error");
        var handler = CrearProbarHandler();

        var resultado = await handler.Handle(new ProbarWebhookCommand("https://mi-tienda.com/api/test-webhook"), CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.Equal(500, resultado.CodigoHttp);
        Assert.Contains("500", resultado.Mensaje);
    }

    [Fact]
    public async Task ProbarWebhook_DestinoInalcanzable_ReportaElErrorSinCodigoHttp()
    {
        _emisor.Respuesta = new ResultadoWebhook(false, null, "Destino no permitido: la URL del aviso apunta a una dirección interna.");
        var handler = CrearProbarHandler();

        var resultado = await handler.Handle(new ProbarWebhookCommand("https://mi-tienda.com/api/test-webhook"), CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.Equal(0, resultado.CodigoHttp);
        Assert.Contains("dirección interna", resultado.Mensaje);
    }

    [Fact]
    public async Task ProbarWebhook_ComercioConSuscripcion_FirmaConSuClaveRealParaQueElReceptorPuedaValidar()
    {
        var comercioId = _tenant.ComercioOperadorId ?? Guid.NewGuid();
        _tenant.ComercioOperadorId = comercioId;
        await _suscripcionesRepo.AddAsync(new SuscripcionAviso(Guid.NewGuid(), comercioId, "EnvioAdmitido", "https://comercio.com/hook", "clave-real-del-comercio"));
        var handler = CrearProbarHandler();

        var resultado = await handler.Handle(new ProbarWebhookCommand("https://otra-url.com/hook"), CancellationToken.None);

        Assert.True(UltimaMilla.Application.Messaging.FirmaWebhook.EsValida("clave-real-del-comercio", resultado.PayloadEnviado, resultado.FirmaGenerada));
    }

    [Fact]
    public async Task ProbarWebhook_UrlInvalida_LanzaDomainValidationException()
    {
        var handler = CrearProbarHandler();
        var comando = new ProbarWebhookCommand("direccion-invalida");

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(comando, CancellationToken.None));
    }

    [Fact]
    public async Task ObtenerConfiguracionAvisos_SinConfiguracionPrevia_RetornaValoresPorDefecto()
    {
        // Arrange
        var handler = CrearObtenerHandler();

        // Act
        var resultado = await handler.Handle(new ObtenerConfiguracionAvisosQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(resultado);
        Assert.False(resultado.EstaConfigurado);
        Assert.Empty(resultado.EventosSuscritos);
        Assert.NotEmpty(resultado.CatalogoEventos);
        Assert.Contains(resultado.CatalogoEventos, e => e.Codigo == "EnvioAdmitido");
        Assert.Contains(resultado.CatalogoEventos, e => e.Codigo == "Entregado");
    }

    [Fact]
    public async Task ObtenerConfiguracionAvisos_ConSuscripcionesExistentes_RetornaDetalleCompleto()
    {
        // Arrange
        var comercioId = _tenant.ComercioOperadorId!.Value;
        var suscripcion1 = new SuscripcionAviso(Guid.NewGuid(), comercioId, "EnvioAdmitido", "https://comercio.com/hook", "secreto123");
        var suscripcion2 = new SuscripcionAviso(Guid.NewGuid(), comercioId, "Entregado", "https://comercio.com/hook", "secreto123");
        await _suscripcionesRepo.AddAsync(suscripcion1);
        await _suscripcionesRepo.AddAsync(suscripcion2);

        var aviso = new AvisoComercio(Guid.NewGuid(), suscripcion1.Id, Guid.NewGuid(), "{\"test\":true}", "firma123", "Entregado");
        await _avisosRepo.AddAsync(aviso);

        var handler = CrearObtenerHandler();

        // Act
        var resultado = await handler.Handle(new ObtenerConfiguracionAvisosQuery(), CancellationToken.None);

        // Assert
        Assert.True(resultado.EstaConfigurado);
        Assert.Equal("https://comercio.com/hook", resultado.UrlWebhook);
        Assert.Equal("secreto123", resultado.ClaveFirma);
        Assert.Equal(2, resultado.EventosSuscritos.Count);
        Assert.Single(resultado.HistorialReciente);
    }
}
