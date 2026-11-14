using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Infrastructure.Persistence;
using DatabaseSeeder = UltimaMilla.Infrastructure.Persistence.DatabaseSeeder;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// El hub de operación (posición de los repartidores y canales personales) respeta el perfil y el aislamiento por operador (letra 6.10).
/// Antes cualquier usuario autenticado, incluso de un comercio, podía enviar la posición de cualquier repartidor, y la recibían las conexiones
/// de todos los operadores; también podía unirse al grupo de otro operador y al canal personal de un repartidor ajeno (D-13, auditoría del 14/11).
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class OperacionHubApiTests
{
    private readonly ApiFactory _factory;

    public OperacionHubApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private sealed record Conexion(HubConnection Hub, ConcurrentBag<JsonElement> Posiciones);

    private async Task<Conexion> ConectarAsync(string email, string password)
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(System.Net.HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

        var manejador = _factory.Server.CreateHandler();
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/operacion"), opciones =>
            {
                opciones.HttpMessageHandlerFactory = _ => manejador;
                opciones.AccessTokenProvider = () => Task.FromResult<string?>(token);
                opciones.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        var posiciones = new ConcurrentBag<JsonElement>();
        hub.On<JsonElement>("PosicionRepartidorActualizada", p => posiciones.Add(p));
        await hub.StartAsync();
        return new Conexion(hub, posiciones);
    }

    private async Task SembrarSegundoOperadorAsync()
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseSeederSegundoOperador.SembrarAsync(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
    }

    [FactPostgres]
    public async Task UnUsuarioDeComercio_NoPuedeTransmitirPosiciones()
    {
        var comercio = await ConectarAsync("comercio@gmail.com", "Comercio.2026!");

        await Assert.ThrowsAsync<HubException>(() =>
            comercio.Hub.InvokeAsync("TransmitirCoordenadas", Guid.Empty, Guid.NewGuid(), -34.9, -56.16, 30.0, 80));
        await comercio.Hub.StopAsync();
    }

    [FactPostgres]
    public async Task LaPosicionDeUnRepartidor_SoloLaRecibenLasConexionesDeSuOperador_NoDeComercioNiDeOtroOperador()
    {
        await SembrarSegundoOperadorAsync();
        var repartidor = await ConectarAsync("repartidor@gmail.com", "Repartidor.2026!");
        var adminDelMismoOperador = await ConectarAsync("admin@gmail.com", "Admin.2026!");
        var adminDeOtroOperador = await ConectarAsync("admin.express@ultimamilla.com.uy", "demo1234");
        var comercio = await ConectarAsync("comercio@gmail.com", "Comercio.2026!");

        await repartidor.Hub.InvokeAsync("TransmitirCoordenadas", Guid.Empty, Guid.Empty, -34.9011, -56.1645, 30.0, 80);
        await Task.Delay(1500);

        // Control: quien administra el mismo operador sí la recibe (si no, la prueba no probaría nada)
        Assert.NotEmpty(adminDelMismoOperador.Posiciones);
        Assert.Empty(adminDeOtroOperador.Posiciones);
        Assert.Empty(comercio.Posiciones);

        foreach (var c in new[] { repartidor, adminDelMismoOperador, adminDeOtroOperador, comercio })
            await c.Hub.StopAsync();
    }

    [FactPostgres]
    public async Task NoSePuedeUnirseAlGrupoDeOtroOperadorNiAlCanalDeOtroRepartidor()
    {
        await SembrarSegundoOperadorAsync();
        var admin = await ConectarAsync("admin@gmail.com", "Admin.2026!");
        var repartidor = await ConectarAsync("repartidor@gmail.com", "Repartidor.2026!");

        await Assert.ThrowsAsync<HubException>(() =>
            admin.Hub.InvokeAsync("SuscribirAOperador", DatabaseSeederSegundoOperador.OperadorId.ToString()));
        // Al propio operador sí
        await admin.Hub.InvokeAsync("SuscribirAOperador", DatabaseSeeder.DefaultOperadorId.ToString());

        // El repartidor no puede registrarse en el canal personal de otro repartidor
        await Assert.ThrowsAsync<HubException>(() => repartidor.Hub.InvokeAsync("RegistrarRepartidor", Guid.NewGuid()));

        await admin.Hub.StopAsync();
        await repartidor.Hub.StopAsync();
    }
}
