using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Cuando un comercio solicita un retiro, al despachador le corresponde planificarlo: su sesión recibe en vivo el aviso "Solicitado"
/// (evento RetiroCambioEstado, que reciben todas las sesiones del personal del operador) y el Backoffice lo muestra con el acceso directo a armar la hoja de ruta.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class RetiroAvisoAlDespachadorApiTests
{
    private readonly ApiFactory _factory;

    public RetiroAvisoAlDespachadorApiTests(ApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Cliente, string Token)> IniciarSesionAsync(string email, string password)
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (cliente, token);
    }

    [FactPostgres]
    public async Task UnRetiroSolicitadoPorUnComercio_SeAvisaEnVivoAlDespachadorDelOperador()
    {
        var (comercio, _) = await IniciarSesionAsync("comercio@gmail.com", "Comercio.2026!");
        var (_, tokenDespachador) = await IniciarSesionAsync("admin@gmail.com", "Admin.2026!");

        var manejador = _factory.Server.CreateHandler();
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/tablero"), o =>
            {
                o.HttpMessageHandlerFactory = _ => manejador;
                o.AccessTokenProvider = () => Task.FromResult<string?>(tokenDespachador);
                o.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        var avisos = new ConcurrentQueue<JsonElement>();
        hub.On<JsonElement>("RetiroCambioEstado", a => avisos.Enqueue(a));
        await hub.StartAsync();
        try
        {
            var solicitud = await comercio.PostAsJsonAsync("/api/comercio/retiros", new
            {
                nombreContacto = "Ana", calle = "Av. Italia", numero = "10", ciudad = "Montevideo", motivo = "Cambio",
                bultos = new[] { new { descripcion = "Camisa", peso = 1, alto = 10, ancho = 10, profundo = 10 } }
            });
            Assert.Equal(HttpStatusCode.Created, solicitud.StatusCode);

            for (var espera = 0; espera < 6000 && avisos.IsEmpty; espera += 100)
                await Task.Delay(100);

            Assert.True(avisos.TryDequeue(out var aviso), "El despachador no recibió el aviso del retiro solicitado.");
            Assert.Equal("Solicitado", aviso.GetProperty("estado").GetString());
            Assert.False(string.IsNullOrWhiteSpace(aviso.GetProperty("codigoSeguimiento").GetString()));
        }
        finally
        {
            await hub.DisposeAsync();
        }
    }
}
