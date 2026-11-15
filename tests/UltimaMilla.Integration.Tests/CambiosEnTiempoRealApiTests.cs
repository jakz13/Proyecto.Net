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
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Todo cambio de datos que la API acepta se avisa en tiempo real a las demás sesiones del operador (evento "DatosCambiaron" del hub de tablero), para que el Backoffice
/// y el portal se pongan al día solos. Antes solo se avisaba de cambios de estado de envíos y rutas: una alta de vehículo, repartidor, zona o tarifa hecha en una sesión no
/// aparecía en la otra hasta recargar.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class CambiosEnTiempoRealApiTests
{
    private readonly ApiFactory _factory;

    public CambiosEnTiempoRealApiTests(ApiFactory factory) => _factory = factory;

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];

    private async Task<(HttpClient Cliente, string Token)> IniciarSesionAsync(string email, string password)
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (cliente, token);
    }

    private async Task<(HubConnection Hub, ConcurrentQueue<JsonElement> Avisos)> EscucharAsync(string token)
    {
        var manejador = _factory.Server.CreateHandler();
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/tablero"), opciones =>
            {
                opciones.HttpMessageHandlerFactory = _ => manejador;
                opciones.AccessTokenProvider = () => Task.FromResult<string?>(token);
                opciones.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        var avisos = new ConcurrentQueue<JsonElement>();
        hub.On<JsonElement>(CambiosEnTiempoRealMiddleware.EventoDatosCambiaron, a => avisos.Enqueue(a));
        await hub.StartAsync();
        return (hub, avisos);
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

    private static object NuevoVehiculo() => new { patente = "T" + Sufijo().ToUpperInvariant(), tipo = "Furgón", capacidadPeso = 500, capacidadVolumen = 5 };

    [FactPostgres]
    public async Task UnaAltaHechaEnUnaSesion_SeAvisaALaOtraSesionDeLaMismaCuenta()
    {
        var (_, tokenOtra) = await IniciarSesionAsync("admin@gmail.com", "Admin.2026!");
        var (sesionQueEscribe, _) = await IniciarSesionAsync("admin@gmail.com", "Admin.2026!");
        var (hub, avisos) = await EscucharAsync(tokenOtra);
        await using var conexionAviso = hub;

        var alta = await sesionQueEscribe.PostAsJsonAsync("/api/v1/flota/vehiculos", NuevoVehiculo());
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        Assert.True(await EsperarAsync(() => !avisos.IsEmpty), "La otra sesión no recibió el aviso de datos cambiados.");
        avisos.TryPeek(out var aviso);
        Assert.Equal("flota", aviso.GetProperty("area").GetString());
        Assert.Equal("POST", aviso.GetProperty("accion").GetString());
    }

    [FactPostgres]
    public async Task UnaLecturaOUnaEscrituraRechazada_NoGeneranAviso()
    {
        var (_, tokenOtra) = await IniciarSesionAsync("admin@gmail.com", "Admin.2026!");
        var (sesionQueEscribe, _) = await IniciarSesionAsync("admin@gmail.com", "Admin.2026!");
        var (hub, avisos) = await EscucharAsync(tokenOtra);
        await using var conexionAviso = hub;

        Assert.Equal(HttpStatusCode.OK, (await sesionQueEscribe.GetAsync("/api/v1/flota/vehiculos")).StatusCode);
        var rechazada = await sesionQueEscribe.PostAsJsonAsync("/api/v1/flota/vehiculos", new { patente = "", tipo = "", capacidadPeso = -1, capacidadVolumen = -1 });
        Assert.True((int)rechazada.StatusCode is >= 400 and < 500);

        await Task.Delay(1200);
        Assert.True(avisos.IsEmpty, "Una lectura o una escritura rechazada no debe avisar a las demás sesiones.");
    }

    [FactPostgres]
    public async Task ElAviso_NoCruzaAOtroOperador()
    {
        using (var alcance = _factory.Services.CreateScope())
        {
            _ = _factory.CreateClient();
            await DatabaseSeederSegundoOperador.SembrarAsync(alcance.ServiceProvider.GetRequiredService<AppDbContext>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        }

        var (_, tokenOperadorUno) = await IniciarSesionAsync("admin@gmail.com", "Admin.2026!");
        var (adminDos, _) = await IniciarSesionAsync("admin2@gmail.com", "Admin.2026!");
        var (hub, avisos) = await EscucharAsync(tokenOperadorUno);
        await using var conexionAviso = hub;

        var alta = await adminDos.PostAsJsonAsync("/api/v1/flota/vehiculos", NuevoVehiculo());
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        await Task.Delay(1500);
        Assert.True(avisos.IsEmpty, "El operador 1 recibió un aviso de un cambio del operador 2.");
    }

    [Theory]
    [InlineData("POST", "/api/v1/flota/vehiculos", 201, true)]
    [InlineData("PUT", "/api/v1/zonas-cobertura/abc", 200, true)]
    [InlineData("PATCH", "/api/envios/abc", 204, true)]
    [InlineData("DELETE", "/api/usuarios/abc", 204, true)]
    [InlineData("GET", "/api/v1/flota/vehiculos", 200, false)]
    [InlineData("POST", "/api/v1/flota/vehiculos", 400, false)]
    [InlineData("POST", "/api/v1/flota/vehiculos", 500, false)]
    [InlineData("POST", "/api/v1/auth/login", 200, false)]
    [InlineData("POST", "/api/mobile/v1/posiciones", 200, false)]
    [InlineData("POST", "/hubs/tablero/negotiate", 200, false)]
    [InlineData("POST", "/tiempo-real/token", 200, false)]
    public void SoloLasEscriturasAceptadasDeLaApiSeAvisan(string metodo, string ruta, int estado, bool avisa)
        => Assert.Equal(avisa, CambiosEnTiempoRealMiddleware.EsCambioDeDatos(metodo, new PathString(ruta), estado));

    [Theory]
    [InlineData("/api/v1/flota/vehiculos", "flota")]
    [InlineData("/api/v2/Zonas-Cobertura/123", "zonas-cobertura")]
    [InlineData("/api/envios/5", "envios")]
    [InlineData("/api/usuarios", "usuarios")]
    [InlineData("/api", "general")]
    public void ElAreaSeDeduceDeLaRuta(string ruta, string area)
        => Assert.Equal(area, CambiosEnTiempoRealMiddleware.AreaDe(new PathString(ruta)));
}
