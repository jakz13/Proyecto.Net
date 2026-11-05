using System.Text.Json;
using UltimaMilla.Application.Commands.Mensajeria;
using UltimaMilla.Application.Commands.Mensajeria.ProcesarMensaje;
using UltimaMilla.Application.Commands.Operador.GestionTarifas;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Operador.GestionTarifas;
using UltimaMilla.Application.Queries.Seguimiento.ConsultarSeguimientoPublico;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Tests;

/// <summary>
/// Pruebas unitarias exhaustivas del caché distribuido (RNF 6.7 y Aclaración 3.1):
/// verifica aciertos, fallos, invalidación entre instancias, aislamiento por operador y degradación elegante ante caída del caché.
/// </summary>
public class CacheDistribuidoTests
{
    private static readonly Guid Operador1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Operador2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Comercio1Id = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task SeguimientoPublico_PrimeraLecturaEsFalloYSegundaEsAciertoDesdeCache()
    {
        MedidorCache.ReiniciarParaPruebas();
        var cacheCompartido = new FakeCacheDistribuidoCompartido();
        var envioRepo = new FakeEnvioRepository();
        var franjaRepo = new FakeFranjaHorariaRepository();

        var envio = new Envio(
            Guid.NewGuid(),
            Comercio1Id,
            Guid.NewGuid(),
            "Estándar",
            "UM-CACHE-001",
            direccionEntregaId: Guid.NewGuid(),
            operadorId: Operador1Id);
        envio.AgregarBulto("BLT-CACHE-01", 2.0m);
        envioRepo.Envios.Add(envio);

        var handler = new ConsultarSeguimientoPublicoQueryHandler(envioRepo, franjaRepo, cacheCompartido);

        // 1. Primera consulta: fallo de caché, lee del repositorio y almacena en el caché distribuido
        var primera = await handler.Handle(new ConsultarSeguimientoPublicoQuery("um-cache-001"), CancellationToken.None);
        Assert.NotNull(primera);
        Assert.Equal("UM-CACHE-001", primera!.CodigoSeguimiento);
        Assert.Equal(EstadoEnvio.Admitido, primera.EstadoActual);

        // 2. Se quita el envío del repositorio para comprobar que la segunda lectura proviene efectivamente del caché
        envioRepo.Envios.Clear();

        var segunda = await handler.Handle(new ConsultarSeguimientoPublicoQuery("UM-CACHE-001"), CancellationToken.None);
        Assert.NotNull(segunda);
        Assert.Equal("UM-CACHE-001", segunda!.CodigoSeguimiento);
        Assert.Equal(EstadoEnvio.Admitido, segunda.EstadoActual);

        var metricas = MedidorCache.ObtenerResumen("Redis")
            .PorCasoDeUso.Single(m => m.CasoDeUso == ClavesCache.CasoUsoSeguimientoPublico);
        Assert.True(metricas.Fallos >= 1);
        Assert.True(metricas.Aciertos >= 1);
        Assert.True(metricas.TasaAciertosPorcentaje > 0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SeguimientoPublico_CodigosNulosOVacios_NoConsultanNiContaminanElCache(string? codigoInvalido)
    {
        var cache = new FakeCacheDistribuidoCompartido();
        var handler = new ConsultarSeguimientoPublicoQueryHandler(new FakeEnvioRepository(), new FakeFranjaHorariaRepository(), cache);

        var resultado = await handler.Handle(new ConsultarSeguimientoPublicoQuery(codigoInvalido!), CancellationToken.None);

        Assert.Null(resultado);
        Assert.Empty(cache.ClavesConsultadas);
        Assert.Empty(cache.ClavesEscritas);
    }

    [Fact]
    public async Task SeguimientoPublico_ProcesamientoDeEventoEnWorker_InvalidaEntradaEnCacheDeInmediato()
    {
        var cacheCompartido = new FakeCacheDistribuidoCompartido();
        var envioRepo = new FakeEnvioRepository();
        var franjaRepo = new FakeFranjaHorariaRepository();

        var envio = new Envio(
            Guid.NewGuid(),
            Comercio1Id,
            Guid.NewGuid(),
            "Estándar",
            "UM-WORKER-CACHE",
            direccionEntregaId: Guid.NewGuid(),
            operadorId: Operador1Id);
        envio.AgregarBulto("BLT-W-01", 1.5m);
        envioRepo.Envios.Add(envio);

        var seguimientoHandler = new ConsultarSeguimientoPublicoQueryHandler(envioRepo, franjaRepo, cacheCompartido);

        // 1. Calentar el caché en estado Admitido
        var inicial = await seguimientoHandler.Handle(new ConsultarSeguimientoPublicoQuery("UM-WORKER-CACHE"), CancellationToken.None);
        Assert.NotNull(inicial);
        Assert.Equal(EstadoEnvio.Admitido, inicial!.EstadoActual);

        // 2. El envío cambia a EnDeposito y el Worker procesa el evento publicado en la cola
        envio.RecepcionarEnDeposito(new[] { "BLT-W-01" }, null, null, Guid.NewGuid());

        var eventoPublicado = new EventoEnvioPublicado(
            Operador1Id,
            envio.Id,
            Comercio1Id,
            envio.CodigoSeguimientoPublico,
            "RecepcionEnDeposito",
            EstadoEnvio.Admitido.ToString(),
            EstadoEnvio.EnDeposito.ToString(),
            DateTime.UtcNow,
            "corr-cache-test");

        var avisoRepo = new FakeAvisoComercioRepositoryLocal();
        var uow = new FakeUnitOfWork();
        var workerHandler = new ProcesarMensajeCommandHandler(
            new FakeIdempotenciaRepositoryLocal(),
            envioRepo,
            new FakeDestinatarioRepository(),
            new FakeNotificacionRepositoryLocal(),
            new FakeNotificacionDestinatarioServiceLocal(),
            avisoRepo,
            new AvisoComercioEntregador(avisoRepo, new FakeWebhookSenderLocal(), uow, new FakeClock()),
            uow,
            new FakeCurrentTenant { OperadorId = Operador1Id },
            cacheCompartido);

        var resultadoWorker = await workerHandler.Handle(
            new ProcesarMensajeCommand(Guid.NewGuid(), nameof(Envio), envio.Id, EventoEnvioPublicado.TipoMensaje, eventoPublicado.Serializar()),
            CancellationToken.None);

        Assert.Equal(EstadoProcesamientoMensaje.Procesado, resultadoWorker.Estado);
        Assert.Contains("seguimiento:UM-WORKER-CACHE", cacheCompartido.ClavesEliminadas);

        // 3. Al volver a consultar el seguimiento público, ya no devuelve el estado viejo del caché sino EnDeposito
        var actualizado = await seguimientoHandler.Handle(new ConsultarSeguimientoPublicoQuery("UM-WORKER-CACHE"), CancellationToken.None);
        Assert.NotNull(actualizado);
        Assert.Equal(EstadoEnvio.EnDeposito, actualizado!.EstadoActual);
    }

    [Fact]
    public async Task CuadroTarifario_PublicarEnInstancia1_InvalidaCacheYInstancia2RespondeDeInmediatoConValorNuevo()
    {
        MedidorCache.ReiniciarParaPruebas();
        var cacheCompartido = new FakeCacheDistribuidoCompartido();
        var cuadroRepo = new FakeCuadroTarifarioRepository();
        var zonaRepo = new FakeZonaCoberturaRepository();
        var tenantOp1 = new FakeCurrentTenant { OperadorId = Operador1Id };
        var uow = new FakeUnitOfWork();

        var version1 = new CuadroTarifario(Guid.NewGuid(), Operador1Id, 1, DateTime.UtcNow.AddDays(-5));
        version1.AgregarRegla(new ReglaTarifa(Guid.NewGuid(), version1.Id, "Estándar", 0m, 10m, 0m, 1m, 150m, 0m, 0m));
        await cuadroRepo.AgregarAsync(version1);

        // Instancia 1 e Instancia 2 comparten el mismo almacén de caché distribuido
        var consultaInstancia2 = new ObtenerCuadroTarifarioVigenteQueryHandler(cuadroRepo, zonaRepo, tenantOp1, cacheCompartido);
        var publicadorInstancia1 = new PublicarVersionCuadroTarifarioCommandHandler(cuadroRepo, zonaRepo, tenantOp1, uow, cacheCompartido);

        // 1. Instancia 2 consulta la tarifa vigente: fallo inicial y queda almacenada en caché (PrecioBase = 150)
        var lecturaInicial = await consultaInstancia2.Handle(new ObtenerCuadroTarifarioVigenteQuery(), CancellationToken.None);
        Assert.NotNull(lecturaInicial);
        Assert.Equal(1, lecturaInicial!.Version);
        Assert.Equal(150m, lecturaInicial.ReglasTarifa.Single().PrecioFinal);

        // Segunda lectura en Instancia 2: acierto de caché
        var lecturaDesdeCache = await consultaInstancia2.Handle(new ObtenerCuadroTarifarioVigenteQuery(), CancellationToken.None);
        Assert.Equal(1, lecturaDesdeCache!.Version);

        // 2. El administrador publica una nueva tarifa a través de la Instancia 1 (PrecioBase = 240, Recargo = 10 -> 250)
        var comandoNuevaTarifa = new PublicarVersionCuadroTarifarioCommand(
            DateTime.UtcNow.AddMinutes(-1),
            "Actualización en caliente",
            new List<CrearReglaTarifaDto>
            {
                new(null, "Estándar", 0m, 10m, 0m, 1m, 240m, 10m, 0m)
            });

        var publicada = await publicadorInstancia1.Handle(comandoNuevaTarifa, CancellationToken.None);
        Assert.Equal(2, publicada.Version);

        // 3. Instancia 2 consulta de inmediato y recibe la versión 2 con el precio nuevo (250) sin esperar a que expire el TTL
        var lecturaPosteriorEnInstancia2 = await consultaInstancia2.Handle(new ObtenerCuadroTarifarioVigenteQuery(), CancellationToken.None);
        Assert.NotNull(lecturaPosteriorEnInstancia2);
        Assert.Equal(2, lecturaPosteriorEnInstancia2!.Version);
        Assert.Equal(250m, lecturaPosteriorEnInstancia2.ReglasTarifa.Single().PrecioFinal);

        var metricasTarifas = MedidorCache.ObtenerResumen("Redis")
            .PorCasoDeUso.Single(m => m.CasoDeUso == ClavesCache.CasoUsoCuadroTarifario);
        Assert.True(metricasTarifas.Aciertos >= 1);
        Assert.True(metricasTarifas.Fallos >= 2);
        Assert.True(metricasTarifas.Invalidaciones >= 2);
    }

    [Fact]
    public async Task CuadroTarifario_AislamientoMultiInquilino_ClavesSeparadasPorOperadorSinFiltracion()
    {
        var cacheCompartido = new FakeCacheDistribuidoCompartido();
        var cuadroRepo = new FakeCuadroTarifarioRepository();
        var zonaRepo = new FakeZonaCoberturaRepository();

        var cuadroOp1 = new CuadroTarifario(Guid.NewGuid(), Operador1Id, 1, DateTime.UtcNow.AddDays(-2));
        cuadroOp1.AgregarRegla(new ReglaTarifa(Guid.NewGuid(), cuadroOp1.Id, "Estándar", 0m, 10m, 0m, 1m, 100m, 0m, 0m));
        await cuadroRepo.AgregarAsync(cuadroOp1);

        var cuadroOp2 = new CuadroTarifario(Guid.NewGuid(), Operador2Id, 1, DateTime.UtcNow.AddDays(-2));
        cuadroOp2.AgregarRegla(new ReglaTarifa(Guid.NewGuid(), cuadroOp2.Id, "Estándar", 0m, 10m, 0m, 1m, 999m, 0m, 0m));
        await cuadroRepo.AgregarAsync(cuadroOp2);

        var handlerOp1 = new ObtenerCuadroTarifarioVigenteQueryHandler(
            cuadroRepo, zonaRepo, new FakeCurrentTenant { OperadorId = Operador1Id }, cacheCompartido);
        var handlerOp2 = new ObtenerCuadroTarifarioVigenteQueryHandler(
            cuadroRepo, zonaRepo, new FakeCurrentTenant { OperadorId = Operador2Id }, cacheCompartido);

        var vigenteOp1 = await handlerOp1.Handle(new ObtenerCuadroTarifarioVigenteQuery(), CancellationToken.None);
        var vigenteOp2 = await handlerOp2.Handle(new ObtenerCuadroTarifarioVigenteQuery(), CancellationToken.None);

        Assert.NotNull(vigenteOp1);
        Assert.NotNull(vigenteOp2);
        Assert.Equal(Operador1Id, vigenteOp1!.OperadorId);
        Assert.Equal(100m, vigenteOp1.ReglasTarifa.Single().PrecioBase);
        Assert.Equal(Operador2Id, vigenteOp2!.OperadorId);
        Assert.Equal(999m, vigenteOp2.ReglasTarifa.Single().PrecioBase);
    }

    [Fact]
    public async Task Resiliencia_CuandoCacheDegrada_ContinuaFuncionandoContraBaseDeDatos()
    {
        MedidorCache.ReiniciarParaPruebas();
        var cacheDegradado = new FakeCacheDistribuidoCompartido { SimularFallaDeConexion = true };
        var cuadroRepo = new FakeCuadroTarifarioRepository();
        var zonaRepo = new FakeZonaCoberturaRepository();
        var tenant = new FakeCurrentTenant { OperadorId = Operador1Id };

        var cuadro = new CuadroTarifario(Guid.NewGuid(), Operador1Id, 1, DateTime.UtcNow.AddDays(-1));
        cuadro.AgregarRegla(new ReglaTarifa(Guid.NewGuid(), cuadro.Id, "Estándar", 0m, 5m, 0m, 1m, 180m, 0m, 0m));
        await cuadroRepo.AgregarAsync(cuadro);

        var handlerTarifas = new ObtenerCuadroTarifarioVigenteQueryHandler(cuadroRepo, zonaRepo, tenant, cacheDegradado);
        var resultado = await handlerTarifas.Handle(new ObtenerCuadroTarifarioVigenteQuery(), CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal(180m, resultado!.ReglasTarifa.Single().PrecioBase);

        var resumen = MedidorCache.ObtenerResumen("Redis");
        Assert.True(resumen.TotalErroresDeConexion >= 1);
    }

    private sealed class FakeCacheDistribuidoCompartido : IDistributedCacheService
    {
        private readonly Dictionary<string, string> _almacen = new(StringComparer.Ordinal);

        public bool SimularFallaDeConexion { get; set; }
        public List<string> ClavesConsultadas { get; } = new();
        public List<string> ClavesEscritas { get; } = new();
        public List<string> ClavesEliminadas { get; } = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            ClavesConsultadas.Add(key);
            if (SimularFallaDeConexion)
            {
                MedidorCache.RegistrarError(key);
                MedidorCache.RegistrarFallo(key);
                return Task.FromResult<T?>(default);
            }

            if (!_almacen.TryGetValue(key, out var json))
            {
                MedidorCache.RegistrarFallo(key);
                return Task.FromResult<T?>(default);
            }

            MedidorCache.RegistrarAcierto(key);
            return Task.FromResult(JsonSerializer.Deserialize<T>(json));
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            ClavesEscritas.Add(key);
            if (SimularFallaDeConexion)
            {
                MedidorCache.RegistrarError(key);
                return Task.CompletedTask;
            }

            _almacen[key] = JsonSerializer.Serialize(value);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            ClavesEliminadas.Add(key);
            if (SimularFallaDeConexion)
            {
                MedidorCache.RegistrarError(key);
                return Task.CompletedTask;
            }

            _almacen.Remove(key);
            MedidorCache.RegistrarInvalidacion(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeIdempotenciaRepositoryLocal : IIdempotenciaConsumidorRepository
    {
        private readonly List<ClaveIdempotenciaConsumidor> _claves = new();
        public Task<bool> ExisteAsync(string mensajeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_claves.Any(c => c.MensajeId == mensajeId));
        public Task AddAsync(ClaveIdempotenciaConsumidor clave, CancellationToken cancellationToken = default)
        {
            _claves.Add(clave);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotificacionRepositoryLocal : INotificacionRepository
    {
        public Task AddAsync(Notificacion notificacion, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeNotificacionDestinatarioServiceLocal : INotificacionDestinatarioService
    {
        public Task EnviarAsync(NotificacionDestinatario notificacion, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAvisoComercioRepositoryLocal : IAvisoComercioRepository
    {
        public Task<List<SuscripcionAviso>> ListarSuscripcionesActivasAsync(Guid comercioOperadorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<SuscripcionAviso>());
        public Task<SuscripcionAviso?> GetSuscripcionAsync(Guid suscripcionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SuscripcionAviso?>(null);
        public Task<List<SuscripcionAviso>> ListarSuscripcionesAsync(Guid? comercioOperadorId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<SuscripcionAviso>());
        public Task<SuscripcionAviso?> GetSuscripcionParaEdicionAsync(Guid suscripcionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SuscripcionAviso?>(null);
        public Task AddSuscripcionAsync(SuscripcionAviso suscripcion, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<AvisoComercio>> ListarHistorialAvisosAsync(Guid? comercioOperadorId = null, string? estado = null, int limite = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AvisoComercio>());
        public Task<AvisoComercio?> GetAvisoByIdAsync(Guid avisoId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AvisoComercio?>(null);
        public Task AddAsync(AvisoComercio aviso, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<AvisoComercio>> ListarPendientesDeEntregaAsync(DateTime ahoraUtc, int maximo, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AvisoComercio>());
    }

    private sealed class FakeWebhookSenderLocal : IWebhookSender
    {
        public Task<ResultadoWebhook> EnviarAsync(string urlWebhook, string payload, string firma, Guid avisoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResultadoWebhook(true, 200, "HTTP 200 OK"));
    }
}
