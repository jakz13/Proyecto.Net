using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Commands.Identidad.IniciarSesion;
using UltimaMilla.Application.Commands.Mensajeria.GestionarWebhooks;
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
    public async Task PosicionVehiculo_SeEmiteSoloAlTableroDelOperadorPropietario()
    {
        var http = _factory.CreateClient();

        var loginOp1 = await http.PostAsJsonAsync("/api/v1/auth/login", new IniciarSesionRequest("admin@gmail.com", "Admin.2026!"));
        var tokenOp1 = (await loginOp1.Content.ReadFromJsonAsync<IniciarSesionResultadoDto>())!.Token;
        var loginOp2 = await http.PostAsJsonAsync("/api/v1/auth/login", new IniciarSesionRequest("admin2@gmail.com", "Admin.2026!"));
        var tokenOp2 = (await loginOp2.Content.ReadFromJsonAsync<IniciarSesionResultadoDto>())!.Token;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var wsOp1 = await _factory.Server.CreateWebSocketClient().ConnectAsync(
            new Uri($"ws://localhost/hubs/tablero?access_token={Uri.EscapeDataString(tokenOp1)}"), cts.Token);
        await EnviarHandshakeSignalRAsync(wsOp1, cts.Token);
        await LeerTramasHastaContenerAsync(wsOp1, "ConectadoTablero", TimeSpan.FromSeconds(3));

        var wsOp2 = await _factory.Server.CreateWebSocketClient().ConnectAsync(
            new Uri($"ws://localhost/hubs/tablero?access_token={Uri.EscapeDataString(tokenOp2)}"), cts.Token);
        await EnviarHandshakeSignalRAsync(wsOp2, cts.Token);
        await LeerTramasHastaContenerAsync(wsOp2, "ConectadoTablero", TimeSpan.FromSeconds(3));

        var vehiculoOp1 = Guid.NewGuid();
        var vehiculoOp2 = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var notifier = scope.ServiceProvider.GetRequiredService<ITableroRealtimeNotifier>();
            await notifier.NotificarPosicionVehiculoAsync(DatabaseSeeder.DefaultOperadorId, vehiculoOp1, null, -34.9040, -56.1700, cts.Token);
            await notifier.NotificarPosicionVehiculoAsync(DatabaseSeeder.Operador2Id, vehiculoOp2, null, -34.8500, -56.0100, cts.Token);
        }

        var tramasOp1 = await LeerTramasHastaContenerAsync(wsOp1, vehiculoOp1.ToString(), TimeSpan.FromSeconds(3));
        var tramasOp2 = await LeerTramasHastaContenerAsync(wsOp2, vehiculoOp2.ToString(), TimeSpan.FromSeconds(3));
        var textoOp1 = string.Concat(tramasOp1);
        var textoOp2 = string.Concat(tramasOp2);

        Assert.Contains("PosicionVehiculo", textoOp1);
        Assert.Contains(vehiculoOp1.ToString(), textoOp1);
        Assert.DoesNotContain(vehiculoOp2.ToString(), textoOp1);

        Assert.Contains("PosicionVehiculo", textoOp2);
        Assert.Contains(vehiculoOp2.ToString(), textoOp2);
        Assert.DoesNotContain(vehiculoOp1.ToString(), textoOp2);

        wsOp1.Dispose();
        wsOp2.Dispose();
    }

    private async Task<ClientWebSocketPrueba> ConectarTableroAsync(string email, string password, CancellationToken ct)
    {
        var http = _factory.CreateClient();
        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new IniciarSesionRequest(email, password), ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<IniciarSesionResultadoDto>(ct))!.Token;

        var ws = await _factory.Server.CreateWebSocketClient().ConnectAsync(
            new Uri($"ws://localhost/hubs/tablero?access_token={Uri.EscapeDataString(token)}"), ct);
        await EnviarHandshakeSignalRAsync(ws, ct);
        var bienvenida = await LeerTramasHastaContenerAsync(ws, "ConectadoTablero", TimeSpan.FromSeconds(3));
        var payloadToken = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payloadToken = payloadToken.PadRight(payloadToken.Length + (4 - payloadToken.Length % 4) % 4, '=');
        Assert.True(
            bienvenida.Any(t => t.Contains("ConectadoTablero", StringComparison.Ordinal)),
            $"El hub cerró la conexión de {email}. Claims: {Encoding.UTF8.GetString(Convert.FromBase64String(payloadToken))}");
        return new ClientWebSocketPrueba(ws);
    }

    private sealed class ClientWebSocketPrueba : IDisposable
    {
        public ClientWebSocketPrueba(WebSocket ws) => Socket = ws;
        public WebSocket Socket { get; }
        public void Dispose() => Socket.Dispose();
    }

    [FactPostgres]
    public async Task EnvioCambioEstado_ElCambioRealEnLaBaseLlegaAlOperadorYComercioDuenos_YNoAOtros()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        using var operador1 = await ConectarTableroAsync("admin@gmail.com", "Admin.2026!", cts.Token);
        using var operador2 = await ConectarTableroAsync("admin2@gmail.com", "Admin.2026!", cts.Token);
        using var comercioDueno = await ConectarTableroAsync("comercio@gmail.com", "Comercio.2026!", cts.Token);

        // Cambio real de estado de un envío del operador 1 / comercio 3333…, dentro de una transacción que luego se descarta
        // para no alterar los datos que usan las demás pruebas
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            using var alcance = db.HabilitarEscrituraAdministrativa();
            await using var transaccion = await db.Database.BeginTransactionAsync(cts.Token);

            var envio = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(
                Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters(db.Envios),
                e => e.Id == DatabaseSeeder.Envio5Id, cts.Token);
            db.Entry(envio).Property(e => e.EstadoActual).CurrentValue = UltimaMilla.Domain.Enums.EstadoEnvio.Extraviado;
            await db.SaveChangesAsync(cts.Token);

            await transaccion.RollbackAsync(cts.Token);
        }

        var tramasOp1 = string.Concat(await LeerTramasHastaContenerAsync(operador1.Socket, "EnvioCambioEstado", TimeSpan.FromSeconds(4)));
        var tramasComercio = string.Concat(await LeerTramasHastaContenerAsync(comercioDueno.Socket, "EnvioCambioEstado", TimeSpan.FromSeconds(4)));
        var tramasOp2 = string.Concat(await LeerTramasHastaContenerAsync(operador2.Socket, "EnvioCambioEstado", TimeSpan.FromSeconds(2)));

        Assert.Contains("EnvioCambioEstado", tramasOp1);
        Assert.Contains(DatabaseSeeder.Envio5Id.ToString(), tramasOp1);
        Assert.Contains("Extraviado", tramasOp1);

        Assert.Contains("EnvioCambioEstado", tramasComercio);
        Assert.Contains(DatabaseSeeder.Envio5Id.ToString(), tramasComercio);

        Assert.DoesNotContain("EnvioCambioEstado", tramasOp2);
    }

    [FactPostgres]
    public async Task RetiroCambioEstado_ElCambioRealEnLaBaseLlegaAlOperadorYComercioDuenos_YNoAOtros()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        using var operador1 = await ConectarTableroAsync("admin@gmail.com", "Admin.2026!", cts.Token);
        using var operador2 = await ConectarTableroAsync("admin2@gmail.com", "Admin.2026!", cts.Token);
        using var comercioDueno = await ConectarTableroAsync("comercio@gmail.com", "Comercio.2026!", cts.Token);

        var retiroId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            using var alcance = db.HabilitarEscrituraAdministrativa();
            await using var transaccion = await db.Database.BeginTransactionAsync(cts.Token);

            // Alta de un retiro del operador 1 / comercio 3333…; luego se descarta para no alterar los datos de las demás pruebas
            var retiro = new UltimaMilla.Domain.Entities.Retiros.Retiro(
                retiroId, DatabaseSeeder.DefaultOperadorId, DatabaseSeeder.DefaultComercioOperadorId, "RET-TIEMPOREAL",
                "Ana Pérez", Guid.NewGuid(), "Av. Italia 1234", "Cambio de talle");
            db.Retiros.Add(retiro);
            await db.SaveChangesAsync(cts.Token);

            await transaccion.RollbackAsync(cts.Token);
        }

        var tramasOp1 = string.Concat(await LeerTramasHastaContenerAsync(operador1.Socket, "RetiroCambioEstado", TimeSpan.FromSeconds(4)));
        var tramasComercio = string.Concat(await LeerTramasHastaContenerAsync(comercioDueno.Socket, "RetiroCambioEstado", TimeSpan.FromSeconds(4)));
        var tramasOp2 = string.Concat(await LeerTramasHastaContenerAsync(operador2.Socket, "RetiroCambioEstado", TimeSpan.FromSeconds(2)));

        Assert.Contains("RetiroCambioEstado", tramasOp1);
        Assert.Contains(retiroId.ToString(), tramasOp1);
        Assert.Contains("Solicitado", tramasOp1);

        Assert.Contains("RetiroCambioEstado", tramasComercio);
        Assert.Contains(retiroId.ToString(), tramasComercio);

        Assert.DoesNotContain("RetiroCambioEstado", tramasOp2);
    }

    [FactPostgres]
    public async Task IncidenciaCambio_UnaIncidenciaNuevaLlegaAlBackofficeDelOperador_YNoAOtrosNiAlComercio()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        using var operador1 = await ConectarTableroAsync("admin@gmail.com", "Admin.2026!", cts.Token);
        using var operador2 = await ConectarTableroAsync("admin2@gmail.com", "Admin.2026!", cts.Token);
        using var comercioDueno = await ConectarTableroAsync("comercio@gmail.com", "Comercio.2026!", cts.Token);

        var incidenciaId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            using var alcance = db.HabilitarEscrituraAdministrativa();
            await using var transaccion = await db.Database.BeginTransactionAsync(cts.Token);

            // Se abre una incidencia y se descarta, para no alterar los datos de las demás pruebas
            db.Incidencias.Add(new UltimaMilla.Domain.Entities.Incidencias.Incidencia(
                incidenciaId, DatabaseSeeder.DefaultOperadorId, null, "AveriaVehiculo", "Prueba de tiempo real", "Alta"));
            await db.SaveChangesAsync(cts.Token);

            await transaccion.RollbackAsync(cts.Token);
        }

        var tramasOp1 = string.Concat(await LeerTramasHastaContenerAsync(operador1.Socket, "IncidenciaCambio", TimeSpan.FromSeconds(4)));
        var tramasOp2 = string.Concat(await LeerTramasHastaContenerAsync(operador2.Socket, "IncidenciaCambio", TimeSpan.FromSeconds(2)));
        var tramasComercio = string.Concat(await LeerTramasHastaContenerAsync(comercioDueno.Socket, "IncidenciaCambio", TimeSpan.FromSeconds(2)));

        Assert.Contains("IncidenciaCambio", tramasOp1);
        Assert.Contains(incidenciaId.ToString(), tramasOp1);
        Assert.DoesNotContain("IncidenciaCambio", tramasOp2);
        Assert.DoesNotContain("IncidenciaCambio", tramasComercio);
    }

    [FactPostgres]
    public async Task EnvioCambioEstado_UnComercioNoRecibeLosEnviosDeOtroComercio()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        using var operador1 = await ConectarTableroAsync("admin@gmail.com", "Admin.2026!", cts.Token);
        using var comercioDueno = await ConectarTableroAsync("comercio@gmail.com", "Comercio.2026!", cts.Token);

        var envioDeOtroComercio = Guid.NewGuid();
        var otroComercioOperador = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var notifier = scope.ServiceProvider.GetRequiredService<ITableroRealtimeNotifier>();
            await notifier.NotificarEnvioCambioEstadoAsync(
                DatabaseSeeder.DefaultOperadorId, otroComercioOperador, envioDeOtroComercio, "ENV-AJENO", "Entregado", cts.Token);
        }

        // El operador ve todos sus envíos; el comercio solo los suyos
        var tramasOp1 = string.Concat(await LeerTramasHastaContenerAsync(operador1.Socket, envioDeOtroComercio.ToString(), TimeSpan.FromSeconds(4)));
        var tramasComercio = string.Concat(await LeerTramasHastaContenerAsync(comercioDueno.Socket, envioDeOtroComercio.ToString(), TimeSpan.FromSeconds(2)));

        Assert.Contains(envioDeOtroComercio.ToString(), tramasOp1);
        Assert.DoesNotContain(envioDeOtroComercio.ToString(), tramasComercio);
    }

    private async Task<WebSocket> ConectarSeguimientoPublicoAsync(CancellationToken ct)
    {
        _ = _factory.CreateClient();
        // Sin token: el seguimiento público no requiere sesión
        var ws = await _factory.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/hubs/seguimiento"), ct);
        await EnviarHandshakeSignalRAsync(ws, ct);
        await LeerTramasHastaContenerAsync(ws, "{}", TimeSpan.FromSeconds(2));
        return ws;
    }

    /// <summary>Invoca un método del hub (protocolo JSON de SignalR) y devuelve el resultado booleano de la finalización.</summary>
    private static async Task<bool?> InvocarSeguirAsync(WebSocket ws, string metodo, string codigo, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var mensaje = $"{{\"type\":1,\"invocationId\":\"{id}\",\"target\":\"{metodo}\",\"arguments\":[\"{codigo}\"]}}\u001e";
        await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(mensaje)), WebSocketMessageType.Text, true, ct);

        var tramas = string.Concat(await LeerTramasHastaContenerAsync(ws, id, TimeSpan.FromSeconds(3)));
        if (tramas.Contains("\"result\":true", StringComparison.Ordinal)) return true;
        if (tramas.Contains("\"result\":false", StringComparison.Ordinal)) return false;
        return null;
    }

    [FactPostgres]
    public async Task SeguimientoPublico_SoloRecibeLaSenalQuienSigueEseCodigo_SinSesionYSinDatosDelEnvio()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var quienSigue = await ConectarSeguimientoPublicoAsync(cts.Token);
        using var otroDestinatario = await ConectarSeguimientoPublicoAsync(cts.Token);

        const string codigoSeguido = "ENV-2026-00112";
        Assert.True(await InvocarSeguirAsync(quienSigue, "Seguir", codigoSeguido, cts.Token));
        Assert.True(await InvocarSeguirAsync(otroDestinatario, "Seguir", "ENV-2026-00111", cts.Token));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            using var alcance = db.HabilitarEscrituraAdministrativa();
            await using var transaccion = await db.Database.BeginTransactionAsync(cts.Token);

            var envio = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(
                Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters(db.Envios),
                e => e.Id == DatabaseSeeder.Envio5Id, cts.Token);
            db.Entry(envio).Property(e => e.EstadoActual).CurrentValue = UltimaMilla.Domain.Enums.EstadoEnvio.Extraviado;
            await db.SaveChangesAsync(cts.Token);

            await transaccion.RollbackAsync(cts.Token);
        }

        var recibido = string.Concat(await LeerTramasHastaContenerAsync(quienSigue, "EnvioActualizado", TimeSpan.FromSeconds(4)));
        var ajeno = string.Concat(await LeerTramasHastaContenerAsync(otroDestinatario, "EnvioActualizado", TimeSpan.FromSeconds(2)));

        Assert.Contains("EnvioActualizado", recibido);
        // La señal no lleva datos del envío: ni estado, ni identificadores internos, ni datos personales
        Assert.DoesNotContain("Extraviado", recibido);
        Assert.DoesNotContain(DatabaseSeeder.Envio5Id.ToString(), recibido);
        Assert.DoesNotContain(codigoSeguido, recibido);
        Assert.DoesNotContain("EnvioActualizado", ajeno);
    }

    [FactPostgres]
    public async Task SeguimientoPublico_NoPermiteSondearCodigos_FormatoInvalidoYLimitePorConexion()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var ws = await ConectarSeguimientoPublicoAsync(cts.Token);

        // Un código inexistente con formato válido responde igual que uno real: no revela si el envío existe
        var inexistente = await InvocarSeguirAsync(ws, "Seguir", "ENV-9999-99999", cts.Token);
        var real = await InvocarSeguirAsync(ws, "Seguir", "ENV-2026-00111", cts.Token);
        Assert.Equal(real, inexistente);
        Assert.True(inexistente);

        // Formato inválido: se rechaza
        Assert.False(await InvocarSeguirAsync(ws, "Seguir", "x", cts.Token));
        Assert.False(await InvocarSeguirAsync(ws, "Seguir", "no valido!", cts.Token));

        // Hay un tope de códigos por conexión (ya van 2): el tercero entra y el cuarto se rechaza
        Assert.True(await InvocarSeguirAsync(ws, "Seguir", "ENV-2026-00112", cts.Token));
        Assert.False(await InvocarSeguirAsync(ws, "Seguir", "ENV-2026-00113", cts.Token));

        // Soltar uno libera cupo
        await InvocarSeguirAsync(ws, "Dejar", "ENV-9999-99999", cts.Token);
        Assert.True(await InvocarSeguirAsync(ws, "Seguir", "ENV-2026-00113", cts.Token));
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

    [FactPostgres]
    public async Task EndpointsWebhooksYCabecerasSeguridad_CicloCompletoYAislamientoPorOperador()
    {
        var cliente = _factory.CreateClient();

        // 1. Verificar cabeceras OWASP (Fase 4)
        var altaResp = await cliente.PostAsJsonAsync(
            "/api/comercio/webhooks/suscripciones",
            new CrearSuscripcionAvisoRequest(
                DatabaseSeeder.DefaultComercioOperadorId,
                "EnvioEntregado",
                "http://localhost:5000/api/testing/webhook-receiver",
                "secreto-demo-comercio"));

        Assert.Equal(HttpStatusCode.Created, altaResp.StatusCode);
        Assert.Equal("nosniff", altaResp.Headers.GetValues("X-Content-Type-Options").FirstOrDefault());
        Assert.Equal("DENY", altaResp.Headers.GetValues("X-Frame-Options").FirstOrDefault());
        Assert.Equal("strict-origin-when-cross-origin", altaResp.Headers.GetValues("Referrer-Policy").FirstOrDefault());
        Assert.True(altaResp.Headers.Contains("Content-Security-Policy"));

        var creada = await altaResp.Content.ReadFromJsonAsync<SuscripcionAvisoDto>();
        Assert.NotNull(creada);
        Assert.True(creada.Activa);

        // 2. Anti-IDOR: Operador 1 no puede registrar suscripción para el comercio del Operador 2
        var intentoCruzado = await cliente.PostAsJsonAsync(
            "/api/comercio/webhooks/suscripciones",
            new CrearSuscripcionAvisoRequest(
                DatabaseSeederSegundoOperador.ComercioOperadorId,
                "*",
                "http://localhost:5000/api/testing/webhook-receiver",
                "secreto-intruso-2026"));
        Assert.True(intentoCruzado.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden);

        // 3. Listar suscripciones y consultar historial de avisos (Fase 3)
        var listaResp = await cliente.GetAsync($"/api/comercio/webhooks/suscripciones?comercioOperadorId={DatabaseSeeder.DefaultComercioOperadorId}");
        Assert.Equal(HttpStatusCode.OK, listaResp.StatusCode);
        var suscripciones = await listaResp.Content.ReadFromJsonAsync<List<SuscripcionAvisoDto>>();
        Assert.NotNull(suscripciones);
        Assert.Contains(suscripciones, s => s.Id == creada.Id && s.Activa);

        var historialResp = await cliente.GetAsync("/api/operacion/webhooks/historial?limite=10");
        Assert.Equal(HttpStatusCode.OK, historialResp.StatusCode);

        // 4. Desactivar suscripción creada
        var bajaResp = await cliente.DeleteAsync($"/api/comercio/webhooks/suscripciones/{creada.Id}");
        Assert.Equal(HttpStatusCode.OK, bajaResp.StatusCode);
        var desactivada = await bajaResp.Content.ReadFromJsonAsync<SuscripcionAvisoDto>();
        Assert.NotNull(desactivada);
        Assert.False(desactivada.Activa);
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
