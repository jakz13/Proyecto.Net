using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Api.Services;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// La app del repartidor se entera en el momento de lo que se hace desde la central (por ejemplo, que el depósito rindió su hoja de ruta),
/// no recién cuando intenta entregar un envío y el servidor se lo rechaza. Se prueba con un cliente SignalR real conectado como repartidor.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class RepartidorTiempoRealApiTests
{
    private readonly ApiFactory _factory;

    public RepartidorTiempoRealApiTests(ApiFactory factory) => _factory = factory;

    private async Task<string> TokenAsync(string email, string password)
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    private async Task<HubConnection> ConectarAsync(string token, string evento, ConcurrentQueue<JsonElement> recibidos)
    {
        var manejador = _factory.Server.CreateHandler();
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/operacion"), opciones =>
            {
                opciones.HttpMessageHandlerFactory = _ => manejador;
                opciones.AccessTokenProvider = () => Task.FromResult<string?>(token);
                opciones.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        hub.On<JsonElement>(evento, datos => recibidos.Enqueue(datos));
        await hub.StartAsync();
        return hub;
    }

    private static async Task<bool> EsperarAsync(Func<bool> condicion, int milisegundos = 6000)
    {
        for (var espera = 0; espera < milisegundos; espera += 100)
        {
            if (condicion())
                return true;
            await Task.Delay(100);
        }
        return condicion();
    }

    [FactPostgres]
    public async Task CuandoElDepositoRindeLaHoja_ElRepartidorRecibeElEstadoRendidaAlInstante()
    {
        var tokenRepartidor = await TokenAsync("repartidor@gmail.com", "Repartidor.2026!");
        var recibidos = new ConcurrentQueue<JsonElement>();
        await using var hub = await ConectarAsync(tokenRepartidor, "CambioEstadoRuta", recibidos);

        // Una hoja de ruta propia del repartidor (las pruebas de integración comparten base: no se depende de la sembrada)
        var rutaId = Guid.NewGuid();
        using (var alcance = _factory.Services.CreateScope())
        {
            if (alcance.ServiceProvider.GetRequiredService<ICurrentTenant>() is UltimaMilla.Infrastructure.Services.CurrentTenant tenant)
                tenant.EstablecerOperadorDeProceso(DatabaseSeeder.DefaultOperadorId);
            var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
            var ruta = new HojaDeRuta(rutaId, DatabaseSeeder.DefaultOperadorId, DateOnly.FromDateTime(DateTime.UtcNow));
            ruta.AsignarRecursos(DatabaseSeeder.DefaultRepartidorId, DatabaseSeeder.DefaultVehiculoId);
            db.HojasDeRuta.Add(ruta);
            await db.SaveChangesAsync();

            // Es lo mismo que llama RendirHojaDeRutaCommandHandler al terminar la rendición
            var notificador = alcance.ServiceProvider.GetRequiredService<ITableroRealtimeNotifier>();
            await notificador.NotificarCambioRutaAsync(DatabaseSeeder.DefaultOperadorId, rutaId, "Rendida");
        }

        // Antes llega el aviso de la hoja recién creada (estado Borrador, que ahora emite SaveChanges): lo que importa es que también llegue "Rendida"
        Assert.True(await EsperarAsync(() => recibidos.Any(r => r.GetProperty("hojaDeRutaId").GetGuid() == rutaId && r.GetProperty("estado").GetString() == "Rendida")),
            "El repartidor no recibió el aviso de ruta rendida.");
    }

    [FactPostgres]
    public async Task UnCambioHechoEnLaCentral_LoRecibeElRepartidorDeSuOperador_YNoElDeOtro()
    {
        using (var alcance = _factory.Services.CreateScope())
        {
            _ = _factory.CreateClient();
            await DatabaseSeederSegundoOperador.SembrarAsync(alcance.ServiceProvider.GetRequiredService<AppDbContext>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        }

        var recibidosRepartidor = new ConcurrentQueue<JsonElement>();
        var recibidosOtroOperador = new ConcurrentQueue<JsonElement>();
        await using var repartidor = await ConectarAsync(await TokenAsync("repartidor@gmail.com", "Repartidor.2026!"),
            CambiosEnTiempoRealMiddleware.EventoDatosCambiaron, recibidosRepartidor);
        await using var otroOperador = await ConectarAsync(await TokenAsync("admin2@gmail.com", "Admin.2026!"),
            CambiosEnTiempoRealMiddleware.EventoDatosCambiaron, recibidosOtroOperador);

        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync("admin@gmail.com", "Admin.2026!"));
        var alta = await admin.PostAsJsonAsync("/api/v1/flota/vehiculos",
            new { patente = "R" + Guid.NewGuid().ToString("N")[..7].ToUpperInvariant(), tipo = "Furgón", capacidadPeso = 500, capacidadVolumen = 5 });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        Assert.True(await EsperarAsync(() => !recibidosRepartidor.IsEmpty), "El repartidor no recibió el aviso de datos cambiados.");
        Assert.Equal("flota", recibidosRepartidor.First().GetProperty("area").GetString());

        await Task.Delay(1200);
        Assert.True(recibidosOtroOperador.IsEmpty, "Un conectado de otro operador recibió un aviso que no era suyo.");
    }

    [Theory]
    [InlineData("flota", true)]
    [InlineData("deposito", true)]
    [InlineData("rutas", true)]
    [InlineData("envios", true)]
    [InlineData("mobile", false)]
    [InlineData("MOBILE", false)]
    public void LasEscriturasHechasDesdeElMovilNoSeReenvianALosDemasRepartidores(string area, bool avisaAlMovil)
        => Assert.Equal(avisaAlMovil, CambiosEnTiempoRealMiddleware.DebeAvisarAlMovil(area));

    [Theory]
    [InlineData("/api/mobile/v1/entregas", "mobile")]
    [InlineData("/api/mobile/v1/deposito/recepcionar", "deposito")]
    [InlineData("/api/mobile/v2/Deposito/recepcionar", "deposito")]
    [InlineData("/api/deposito/recepcion", "deposito")]
    [InlineData("/api/comercio/envios/importar", "comercio")]
    public void ElAreaDeLaEscrituraSeDeduceDeLaRuta_YLaRecepcionMovilEsDelDeposito(string ruta, string area)
        => Assert.Equal(area, CambiosEnTiempoRealMiddleware.AreaDe(new PathString(ruta)));

    [Fact]
    public void LaRecepcionEnDepositoHechaDesdeUnMovil_SeReenviaALosOtrosOperarios()
        => Assert.True(CambiosEnTiempoRealMiddleware.DebeAvisarAlMovil(CambiosEnTiempoRealMiddleware.AreaDe(new PathString("/api/mobile/v1/deposito/recepcionar"))));
}
