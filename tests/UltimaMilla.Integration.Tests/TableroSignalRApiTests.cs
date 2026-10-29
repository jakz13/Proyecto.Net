using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Commands.Identidad.IniciarSesion;
using UltimaMilla.Application.Ports;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Pruebas de integración del Hub de SignalR (<c>/hubs/tablero</c>) y <see cref="ITableroRealtimeNotifier"/>:
/// rechazo de conexiones anónimas o con <c>access_token</c> inválido, aborto de conexiones sin <c>OperadorId</c> válido
/// y aislamiento estricto de eventos por grupo <c>operador_{operadorId}</c>.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class TableroSignalRApiTests
{
    private readonly ApiFactory _factory;

    public TableroSignalRApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [FactPostgres]
    public async Task Negotiate_SinCredenciales_Retorna401Unauthorized()
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.PostAsync(
            "/hubs/tablero/negotiate?negotiateVersion=1",
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [FactPostgres]
    public async Task Negotiate_ConAccessTokenInvalido_Retorna401Unauthorized()
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.PostAsync(
            "/hubs/tablero/negotiate?negotiateVersion=1&access_token=token.jwt.invalido",
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [FactPostgres]
    public async Task ConexionWebSocket_ConOperadorVacio_EsAbortadaPorElHub()
    {
        _ = _factory.CreateClient(); // Asegura inicialización del host
        var wsClient = _factory.Server.CreateWebSocketClient();
        wsClient.ConfigureRequest = req =>
        {
            req.Headers["X-Operador-Id"] = Guid.Empty.ToString();
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var ws = await wsClient.ConnectAsync(new Uri("ws://localhost/hubs/tablero"), cts.Token);

        await EnviarHandshakeSignalRAsync(ws, cts.Token);

        // El Hub debe abortar la conexión al detectar OperadorId == Guid.Empty en OnConnectedAsync
        var tramas = await LeerTramasHastaCierreOTimeoutAsync(ws, TimeSpan.FromSeconds(3));
        Assert.DoesNotContain(tramas, t => t.Contains("ConectadoTablero", StringComparison.Ordinal));
        Assert.DoesNotContain(tramas, t => t.Contains("CambioRuta", StringComparison.Ordinal));
        Assert.True(
            ws.State is WebSocketState.CloseReceived or WebSocketState.Closed or WebSocketState.Aborted,
            $"Se esperaba que la conexión sin operador válido fuera cerrada/abortada, pero quedó en estado {ws.State}.");
    }

    [FactPostgres]
    public async Task NotificacionesSignalR_AisladasPorGrupoOperador_MedianteAccessTokenJwt()
    {
        var http = _factory.CreateClient();

        // 1. Obtener tokens JWT reales de Operador 1 y Operador 2
        var loginOp1 = await http.PostAsJsonAsync("/api/v1/auth/login", new IniciarSesionRequest("admin@gmail.com", "Admin.2026!"));
        Assert.Equal(HttpStatusCode.OK, loginOp1.StatusCode);
        var tokenOp1 = (await loginOp1.Content.ReadFromJsonAsync<IniciarSesionResultadoDto>())!.Token;

        var loginOp2 = await http.PostAsJsonAsync("/api/v1/auth/login", new IniciarSesionRequest("admin2@gmail.com", "Admin.2026!"));
        Assert.Equal(HttpStatusCode.OK, loginOp2.StatusCode);
        var tokenOp2 = (await loginOp2.Content.ReadFromJsonAsync<IniciarSesionResultadoDto>())!.Token;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // 2. Conectar ambos clientes WebSocket usando ?access_token=... y aguardar confirmación de suscripción al grupo en OnConnectedAsync
        var wsClientOp1 = _factory.Server.CreateWebSocketClient();
        using var wsOp1 = await wsClientOp1.ConnectAsync(
            new Uri($"ws://localhost/hubs/tablero?access_token={Uri.EscapeDataString(tokenOp1)}"),
            cts.Token);
        await EnviarHandshakeSignalRAsync(wsOp1, cts.Token);
        var bienvenidaOp1 = await LeerTramasHastaContenerAsync(wsOp1, "ConectadoTablero", TimeSpan.FromSeconds(3));
        Assert.Contains(bienvenidaOp1, t => t.Contains("ConectadoTablero", StringComparison.Ordinal));

        var wsClientOp2 = _factory.Server.CreateWebSocketClient();
        using var wsOp2 = await wsClientOp2.ConnectAsync(
            new Uri($"ws://localhost/hubs/tablero?access_token={Uri.EscapeDataString(tokenOp2)}"),
            cts.Token);
        await EnviarHandshakeSignalRAsync(wsOp2, cts.Token);
        var bienvenidaOp2 = await LeerTramasHastaContenerAsync(wsOp2, "ConectadoTablero", TimeSpan.FromSeconds(3));
        Assert.Contains(bienvenidaOp2, t => t.Contains("ConectadoTablero", StringComparison.Ordinal));

        // 3. Emitir notificaciones para Operador 1 y una notificación testigo para Operador 2
        var rutaOp1Id = Guid.NewGuid();
        var paradaOp1Id = Guid.NewGuid();
        var rutaOp2Id = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var notifier = scope.ServiceProvider.GetRequiredService<ITableroRealtimeNotifier>();

            await notifier.NotificarCambioRutaAsync(
                DatabaseSeeder.DefaultOperadorId,
                rutaOp1Id,
                "Despachada",
                cts.Token);

            await notifier.NotificarHitoParadaAsync(
                DatabaseSeeder.DefaultOperadorId,
                rutaOp1Id,
                paradaOp1Id,
                "Entregada",
                cts.Token);

            await notifier.NotificarCambioRutaAsync(
                DatabaseSeeder.Operador2Id,
                rutaOp2Id,
                "Rendida",
                cts.Token);
        }

        // 4. Leer tramas recibidas por cada conexión
        var tramasOp1 = await LeerTramasHastaContenerAsync(wsOp1, paradaOp1Id.ToString(), TimeSpan.FromSeconds(3));
        var tramasOp2 = await LeerTramasHastaContenerAsync(wsOp2, rutaOp2Id.ToString(), TimeSpan.FromSeconds(3));

        var textoOp1 = string.Join("\n", tramasOp1);
        var textoOp2 = string.Join("\n", tramasOp2);

        // Operador 1 recibe sus dos eventos y NO recibe el de Operador 2
        Assert.Contains("CambioRuta", textoOp1);
        Assert.Contains(rutaOp1Id.ToString(), textoOp1);
        Assert.Contains("Despachada", textoOp1);
        Assert.Contains("HitoParada", textoOp1);
        Assert.Contains(paradaOp1Id.ToString(), textoOp1);
        Assert.Contains("Entregada", textoOp1);
        Assert.DoesNotContain(rutaOp2Id.ToString(), textoOp1);

        // Operador 2 recibe únicamente su evento y NO recibe ninguno de Operador 1
        Assert.Contains("CambioRuta", textoOp2);
        Assert.Contains(rutaOp2Id.ToString(), textoOp2);
        Assert.Contains("Rendida", textoOp2);
        Assert.DoesNotContain(rutaOp1Id.ToString(), textoOp2);
        Assert.DoesNotContain(paradaOp1Id.ToString(), textoOp2);
    }

    [FactPostgres]
    public async Task Notifier_ConParametrosVaciosOInvalidos_NoLanzaExcepcion()
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<ITableroRealtimeNotifier>();

        await notifier.NotificarCambioRutaAsync(Guid.Empty, Guid.NewGuid(), "Despachada");
        await notifier.NotificarCambioRutaAsync(DatabaseSeeder.DefaultOperadorId, Guid.Empty, "Despachada");
        await notifier.NotificarCambioRutaAsync(DatabaseSeeder.DefaultOperadorId, Guid.NewGuid(), "");
        await notifier.NotificarHitoParadaAsync(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "Entregada");
        await notifier.NotificarHitoParadaAsync(DatabaseSeeder.DefaultOperadorId, Guid.Empty, Guid.NewGuid(), "Entregada");
        await notifier.NotificarHitoParadaAsync(DatabaseSeeder.DefaultOperadorId, Guid.NewGuid(), Guid.Empty, "Entregada");
        await notifier.NotificarHitoParadaAsync(DatabaseSeeder.DefaultOperadorId, Guid.NewGuid(), Guid.NewGuid(), "   ");
    }

    private static async Task EnviarHandshakeSignalRAsync(WebSocket ws, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e");
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    private static async Task<List<string>> LeerTramasHastaContenerAsync(WebSocket ws, string textoEsperado, TimeSpan timeout)
    {
        var tramas = new List<string>();
        var buffer = new byte[4096];
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult resultado;
            try
            {
                resultado = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (resultado.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            var texto = Encoding.UTF8.GetString(buffer, 0, resultado.Count);
            foreach (var parte in texto.Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
            {
                tramas.Add(parte);
            }

            if (tramas.Any(t => t.Contains(textoEsperado, StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }
        }

        return tramas;
    }

    private static async Task<List<string>> LeerTramasHastaCierreOTimeoutAsync(WebSocket ws, TimeSpan timeout)
    {
        var tramas = new List<string>();
        var buffer = new byte[4096];
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            try
            {
                var resultado = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (resultado.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                var texto = Encoding.UTF8.GetString(buffer, 0, resultado.Count);
                foreach (var parte in texto.Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
                {
                    tramas.Add(parte);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException)
            {
                break;
            }
        }

        return tramas;
    }
}
