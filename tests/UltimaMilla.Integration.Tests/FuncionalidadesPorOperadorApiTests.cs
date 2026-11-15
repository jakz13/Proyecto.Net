using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>Banderas de funcionalidad por operador (opcional 7.4 y aclaración 3.2): activación sin reiniciar, por operador y auditada.</summary>
[Collection(ColeccionApi.Nombre)]
public class FuncionalidadesPorOperadorApiTests
{
    private readonly ApiFactory _factory;

    public FuncionalidadesPorOperadorApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> ClienteComoAsync(string email, string password)
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        return cliente;
    }

    private static object Solicitud() => new
    {
        nombreContacto = "Ana", calle = "Av. Italia", numero = "10", ciudad = "Montevideo", motivo = "Cambio",
        bultos = new[] { new { descripcion = "Camisa", peso = 1, alto = 10, ancho = 10, profundo = 10 } }
    };

    [FactPostgres]
    public async Task ElAdministradorDeLaPlataforma_DesactivaElRetiroSoloParaUnOperador_YQuedaAuditado()
    {
        var plataforma = await ClienteComoAsync("plataforma@gmail.com", "Plataforma.2026!");
        var comercio = await ClienteComoAsync("comercio@gmail.com", "Comercio.2026!");
        var admin = await ClienteComoAsync("admin@gmail.com", "Admin.2026!");
        var op1 = DatabaseSeeder.DefaultOperadorId;

        // Habilitado por defecto
        Assert.Equal(HttpStatusCode.Created, (await comercio.PostAsJsonAsync("/api/comercio/retiros", Solicitud())).StatusCode);

        // Se desactiva para el operador 1, sin reiniciar nada
        var cambio = await plataforma.PutAsJsonAsync($"/api/plataforma/operadores/{op1}/funcionalidades/retiro-en-domicilio", new { habilitada = false });
        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);

        var rechazo = await comercio.PostAsJsonAsync("/api/comercio/retiros", Solicitud());
        Assert.Equal(HttpStatusCode.BadRequest, rechazo.StatusCode);
        Assert.Contains("no está habilitada", await rechazo.Content.ReadAsStringAsync());

        var consultadas = await admin.GetFromJsonAsync<JsonElement>("/api/funcionalidades");
        Assert.False(consultadas.GetProperty("retiro-en-domicilio").GetBoolean());
        Assert.True(consultadas.GetProperty("entrega-parcial").GetBoolean());

        // El otro operador no se ve afectado
        var admin2 = await ClienteComoAsync("admin2@gmail.com", "Admin.2026!");
        Assert.True((await admin2.GetFromJsonAsync<JsonElement>("/api/funcionalidades")).GetProperty("retiro-en-domicilio").GetBoolean());

        // Queda registro de quién y cuándo
        var historial = await plataforma.GetFromJsonAsync<JsonElement>($"/api/plataforma/operadores/{op1}/funcionalidades/historial");
        var ultimo = historial.EnumerateArray().First(h => h.GetProperty("clave").GetString() == "retiro-en-domicilio");
        Assert.True(ultimo.GetProperty("valorAnterior").GetBoolean());
        Assert.False(ultimo.GetProperty("valorNuevo").GetBoolean());
        Assert.NotEqual(Guid.Empty, ultimo.GetProperty("usuarioId").GetGuid());

        // Se reactiva y vuelve a funcionar
        await plataforma.PutAsJsonAsync($"/api/plataforma/operadores/{op1}/funcionalidades/retiro-en-domicilio", new { habilitada = true });
        Assert.Equal(HttpStatusCode.Created, (await comercio.PostAsJsonAsync("/api/comercio/retiros", Solicitud())).StatusCode);
    }

    private async Task<(HubConnection Hub, ConcurrentQueue<JsonElement> Avisos)> EscucharBanderasAsync(string email, string password)
    {
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        var manejador = _factory.Server.CreateHandler();
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/tablero"), o =>
            {
                o.HttpMessageHandlerFactory = _ => manejador;
                o.AccessTokenProvider = () => Task.FromResult<string?>(token);
                o.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        var avisos = new ConcurrentQueue<JsonElement>();
        hub.On<JsonElement>("FuncionalidadesCambiaron", a => avisos.Enqueue(a));
        await hub.StartAsync();
        return (hub, avisos);
    }

    [FactPostgres]
    public async Task UnCambioDeBandera_SeAvisaEnVivoALasSesionesDelOperadorAfectado_YSoloDeEse()
    {
        var plataforma = await ClienteComoAsync("plataforma@gmail.com", "Plataforma.2026!");
        var op1 = DatabaseSeeder.DefaultOperadorId;
        var ruta = $"/api/plataforma/operadores/{op1}/funcionalidades/entrega-parcial";

        var (hubOp1, avisosOp1) = await EscucharBanderasAsync("admin@gmail.com", "Admin.2026!");
        var (hubOp2, avisosOp2) = await EscucharBanderasAsync("admin2@gmail.com", "Admin.2026!");
        try
        {
            // Se parte de habilitada para que el cambio exista
            await plataforma.PutAsJsonAsync(ruta, new { habilitada = true });
            while (avisosOp1.TryDequeue(out _)) { }

            Assert.Equal(HttpStatusCode.OK, (await plataforma.PutAsJsonAsync(ruta, new { habilitada = false })).StatusCode);

            for (var espera = 0; espera < 6000 && avisosOp1.IsEmpty; espera += 100)
                await Task.Delay(100);

            Assert.True(avisosOp1.TryDequeue(out var aviso), "La sesión del operador afectado no recibió el aviso de la bandera.");
            Assert.Equal("entrega-parcial", aviso.GetProperty("clave").GetString());
            Assert.False(aviso.GetProperty("habilitada").GetBoolean());
            Assert.True(avisosOp2.IsEmpty, "El otro operador no debe enterarse de un cambio que no lo afecta.");
        }
        finally
        {
            await plataforma.PutAsJsonAsync(ruta, new { habilitada = true });
            await hubOp1.DisposeAsync();
            await hubOp2.DisposeAsync();
        }
    }

    [FactPostgres]
    public async Task NoPuedenExistirDosOperadoresConElMismoNombreComercial()
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
        using var _ = db.HabilitarEscrituraAdministrativa();

        var existente = await db.Operadores.IgnoreQueryFilters().AsNoTracking().FirstAsync();
        db.Operadores.Add(new Operador(Guid.NewGuid(), "Duplicado S.A.", existente.NombreComercial));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [FactPostgres]
    public async Task SoloLaPlataformaCambiaBanderas_NiElAdministradorDelOperadorNiElComercio()
    {
        var op1 = DatabaseSeeder.DefaultOperadorId;
        foreach (var (email, password) in new[] { ("admin@gmail.com", "Admin.2026!"), ("comercio@gmail.com", "Comercio.2026!") })
        {
            var cliente = await ClienteComoAsync(email, password);
            var respuesta = await cliente.PutAsJsonAsync($"/api/plataforma/operadores/{op1}/funcionalidades/entrega-parcial", new { habilitada = false });
            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        }

        var plataforma = await ClienteComoAsync("plataforma@gmail.com", "Plataforma.2026!");
        var inexistente = await plataforma.PutAsJsonAsync($"/api/plataforma/operadores/{op1}/funcionalidades/no-existe", new { habilitada = false });
        Assert.Equal(HttpStatusCode.BadRequest, inexistente.StatusCode);
    }
}
