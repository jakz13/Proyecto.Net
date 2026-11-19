using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UltimaMilla.Api.Services;
using UltimaMilla.Infrastructure.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Huecos de sincronización en tiempo real cerrados el 18/11: la instrucción del despachador llegaba a la base y no a la app, el worker (otro proceso) no podía avisar a nadie,
/// y las hojas de ruta solo avisaban si cada caso de uso se acordaba de hacerlo. Se prueba con clientes SignalR reales.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class TiempoRealCompletoApiTests
{
    private readonly ApiFactory _factory;

    public TiempoRealCompletoApiTests(ApiFactory factory) => _factory = factory;

    private static async Task<string> TokenAsync(ApiFactory fabrica, string email, string password)
    {
        var login = await fabrica.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    private static HubConnection Hub(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> fabrica, string ruta, string token)
    {
        var manejador = fabrica.Server.CreateHandler();
        return new HubConnectionBuilder()
            .WithUrl(new Uri(fabrica.Server.BaseAddress, ruta), o =>
            {
                o.HttpMessageHandlerFactory = _ => manejador;
                o.AccessTokenProvider = () => Task.FromResult<string?>(token);
                o.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    private static async Task<bool> EsperarAsync(Func<bool> condicion, int ms = 6000)
    {
        for (var t = 0; t < ms; t += 100)
        {
            if (condicion())
                return true;
            await Task.Delay(100);
        }
        return condicion();
    }

    [FactPostgres]
    public async Task LaInstruccionDelDespachador_LlegaALaAppDelRepartidorComoNotificacionOperativa()
    {
        var recibidas = new ConcurrentQueue<(string Titulo, string Mensaje)>();
        await using var repartidor = Hub(_factory, "/hubs/operacion", await TokenAsync(_factory, "repartidor@gmail.com", "Repartidor.2026!"));
        repartidor.On<string, string>("NotificacionOperativa", (titulo, mensaje) => recibidas.Enqueue((titulo, mensaje)));
        await repartidor.StartAsync();

        var despachador = await UsuariosDePrueba.DespachadorAsync(_factory);
        var mensaje = "Cliente cerrado hasta las 15: pase a la siguiente parada " + Guid.NewGuid().ToString("N")[..6];
        var respuesta = await despachador.PostAsJsonAsync("/api/operacion/tablero/instrucciones", new { repartidorId = DatabaseSeeder.DefaultRepartidorId, mensaje });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        Assert.True(await EsperarAsync(() => recibidas.Any(r => r.Mensaje == mensaje)), "La instrucción no llegó a la app del repartidor.");
    }

    [FactPostgres]
    public async Task UnaHojaDeRutaNuevaSeAvisaAlTablero_AunqueNingunCasoDeUsoLlameAlNotificador()
    {
        var cambios = new ConcurrentQueue<JsonElement>();
        await using var tablero = Hub(_factory, "/hubs/tablero", await TokenAsync(_factory, "admin@gmail.com", "Admin.2026!"));
        tablero.On<JsonElement>("CambioRuta", c => cambios.Enqueue(c));
        await tablero.StartAsync();

        var rutaId = Guid.NewGuid();
        using (var alcance = _factory.Services.CreateScope())
        {
            if (alcance.ServiceProvider.GetRequiredService<ICurrentTenant>() is UltimaMilla.Infrastructure.Services.CurrentTenant tenant)
                tenant.EstablecerOperadorDeProceso(DatabaseSeeder.DefaultOperadorId);
            var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
            db.HojasDeRuta.Add(new HojaDeRuta(rutaId, DatabaseSeeder.DefaultOperadorId, DateOnly.FromDateTime(DateTime.UtcNow)));
            await db.SaveChangesAsync();
        }

        Assert.True(await EsperarAsync(() => cambios.Any(c => c.GetProperty("hojaDeRutaId").GetGuid() == rutaId)),
            "SaveChanges no avisó de la hoja de ruta nueva.");
    }

    // ------------------------------------------------------------------------------------------------------------------------
    // Worker -> cola -> API -> SignalR. El worker se comunica solo por la cola de mensajes (letra, 6.1): no llama a la API ni tiene los hubs.
    // ------------------------------------------------------------------------------------------------------------------------

    private TiempoRealConsumerBackgroundService ConsumidorDeLaApi()
    {
        _ = _factory.CreateClient();   // arranca el host
        return _factory.Services.GetServices<IHostedService>().OfType<TiempoRealConsumerBackgroundService>().Single();
    }

    [Fact]
    public void ElAvisoDeTiempoReal_ViajaPorLaColaSinPerderNada()
    {
        var original = new AvisoTiempoReal(AvisoTiempoReal.TipoEnvio, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Codigo: "UM-123", Estado: "Entregado");

        var recibido = AvisoTiempoReal.Deserializar(original.Serializar());

        Assert.Equal(original, recibido);
        Assert.Null(AvisoTiempoReal.Deserializar("esto no es json"u8.ToArray()));
    }

    [FactPostgres]
    public async Task UnAvisoQueLlegaPorLaCola_LaApiLoDifundeAlOperadorCorrecto_YNoAOtro()
    {
        using (var alcance = _factory.Services.CreateScope())
        {
            _ = _factory.CreateClient();
            await DatabaseSeederSegundoOperador.SembrarAsync(alcance.ServiceProvider.GetRequiredService<AppDbContext>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        }

        var delOperadorUno = new ConcurrentQueue<JsonElement>();
        var delOperadorDos = new ConcurrentQueue<JsonElement>();
        await using var admin1 = Hub(_factory, "/hubs/tablero", await TokenAsync(_factory, "admin@gmail.com", "Admin.2026!"));
        await using var admin2 = Hub(_factory, "/hubs/tablero", await TokenAsync(_factory, "admin2@gmail.com", "Admin.2026!"));
        admin1.On<JsonElement>("DatosCambiaron", a => delOperadorUno.Enqueue(a));
        admin2.On<JsonElement>("DatosCambiaron", a => delOperadorDos.Enqueue(a));
        await admin1.StartAsync();
        await admin2.StartAsync();

        var aviso = new AvisoTiempoReal(AvisoTiempoReal.TipoDatos, OperadorId: DatabaseSeeder.DefaultOperadorId, Area: "mensajeria");
        await ConsumidorDeLaApi().ProcesarAsync(aviso.Serializar(), CancellationToken.None);

        Assert.True(await EsperarAsync(() => delOperadorUno.Any(a => a.GetProperty("area").GetString() == "mensajeria")), "El operador 1 no recibió el aviso del worker.");
        await Task.Delay(800);
        Assert.True(delOperadorDos.IsEmpty, "El operador 2 recibió un aviso del operador 1.");
    }

    [FactPostgres]
    public async Task UnAvisoIlegibleODeTipoDesconocido_SeDescartaSinRomperNada()
    {
        var consumidor = ConsumidorDeLaApi();

        await consumidor.ProcesarAsync("basura"u8.ToArray(), CancellationToken.None);
        await consumidor.ProcesarAsync(new AvisoTiempoReal("tipo-inexistente").Serializar(), CancellationToken.None);
    }

    /// <summary>De punta a punta: el notificador del worker deja el aviso en RabbitMQ, la API lo lee de la cola y lo difunde por SignalR. Necesita RabbitMQ local.</summary>
    [FactPostgresYRabbit]
    public async Task DeLaColaDelWorkerAlNavegador_ElAvisoLlegaPorSignalR()
    {
        var avisos = new ConcurrentQueue<JsonElement>();
        await using var tablero = Hub(_factory, "/hubs/tablero", await TokenAsync(_factory, "admin@gmail.com", "Admin.2026!"));
        tablero.On<JsonElement>("DatosCambiaron", a => avisos.Enqueue(a));
        await tablero.StartAsync();
        _ = ConsumidorDeLaApi();

        await using var conexion = new RabbitMqConnectionProvider(Options.Create(new RabbitMqOptions()));
        await using var notificadorDelWorker = new ColaTiempoRealNotifier(conexion, NullLogger<ColaTiempoRealNotifier>.Instance);
        var area = "avisos-" + Guid.NewGuid().ToString("N")[..6];
        await notificadorDelWorker.NotificarDatosCambiaronAsync(DatabaseSeeder.DefaultOperadorId, area);

        Assert.True(await EsperarAsync(() => avisos.Any(a => a.GetProperty("area").GetString() == area), 15000),
            "El aviso que el worker dejó en la cola no llegó al navegador.");
    }
}

/// <summary>Además de Postgres, hace falta un RabbitMQ en localhost:5672 (el del docker-compose de desarrollo). El pipeline no lo tiene y la prueba se omite.</summary>
public sealed class FactPostgresYRabbitAttribute : FactAttribute
{
    public FactPostgresYRabbitAttribute()
    {
        if (!ApiFactory.PostgresDisponible())
        {
            Skip = "No hay un Postgres disponible (ULTIMAMILLA_TEST_PG).";
            return;
        }

        try
        {
            using var cliente = new System.Net.Sockets.TcpClient();
            if (!cliente.ConnectAsync("localhost", 5672).Wait(TimeSpan.FromMilliseconds(800)))
                Skip = "No hay un RabbitMQ en localhost:5672.";
        }
        catch
        {
            Skip = "No hay un RabbitMQ en localhost:5672.";
        }
    }
}
