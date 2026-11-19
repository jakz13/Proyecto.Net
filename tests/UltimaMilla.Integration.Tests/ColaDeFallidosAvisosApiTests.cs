using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Infrastructure.Persistence;
using DatabaseSeeder = UltimaMilla.Infrastructure.Persistence.DatabaseSeeder;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Letra 6.8 y 6.9: reintentos con espera creciente y cola de fallidos que el administrador puede consultar y reintentar.
/// Cuando el comercio responde con error (por ejemplo 500) y los reintentos automáticos se agotan, el aviso tiene que aparecer en esa cola
/// (antes quedaba solo en el historial del portal y el administrador no lo veía), con aislamiento por operador y sin duplicar su estado.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class ColaDeFallidosAvisosApiTests
{
    private const string Cola = "/api/v1/mensajeria/cola-fallidos";
    private const string TipoAviso = "NotificacionWebhookComercio";

    private readonly ApiFactory _factory;

    public ColaDeFallidosAvisosApiTests(ApiFactory factory)
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

    private Task<HttpClient> AdminAsync() => ClienteComoAsync("admin@gmail.com", "Admin.2026!");

    private AppDbContext NuevoContexto(IServiceScope scope)
    {
        _ = _factory.CreateClient(); // asegura migraciones y semillas aplicadas
        return new AppDbContext(scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>(), null);
    }

    /// <summary>Un aviso del comercio demo del operador 1 con sus reintentos automáticos ya agotados por un 500 del comercio.</summary>
    private async Task<Guid> CrearAvisoAgotadoAsync()
    {
        using var scope = _factory.Services.CreateScope();
        using var db = NuevoContexto(scope);
        using var _ = db.HabilitarEscrituraAdministrativa();

        // Destino interno: el envío manual falla de inmediato por la protección anti-SSRF, sin depender de la red
        var suscripcion = new SuscripcionAviso(Guid.NewGuid(), DatabaseSeeder.DefaultComercioOperadorId, "EnvioAdmitido", "http://127.0.0.1:9/hook", "clave-secreta-de-prueba");
        var aviso = new AvisoComercio(Guid.NewGuid(), suscripcion.Id, Guid.NewGuid(), "{\"evento\":\"EnvioPublicado\"}", "firma");
        aviso.MarcarAgotado("HTTP 500 Internal Server Error");

        db.SuscripcionesAviso.Add(suscripcion);
        db.AvisosComercio.Add(aviso);
        await db.SaveChangesAsync();
        return aviso.Id;
    }

    private async Task<AvisoComercio> LeerAvisoAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        using var db = NuevoContexto(scope);
        return await db.AvisosComercio.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == id);
    }

    private static async Task<List<JsonElement>> ListarAsync(HttpClient cliente, string consulta = "")
    {
        var respuesta = await cliente.GetAsync(Cola + "/" + consulta);
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {Cola}/{consulta} -> {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
        return (await respuesta.Content.ReadFromJsonAsync<List<JsonElement>>())!;
    }

    private static bool Contiene(IEnumerable<JsonElement> lista, Guid id) =>
        lista.Any(e => e.GetProperty("id").GetGuid() == id);

    [FactPostgres]
    public async Task UnAvisoAgotado_ApareceEnLaColaDeFallidosConElTipoQueLaPantallaReconoce()
    {
        var id = await CrearAvisoAgotadoAsync();
        var admin = await AdminAsync();

        var lista = await ListarAsync(admin);
        var item = lista.Single(e => e.GetProperty("id").GetGuid() == id);

        Assert.Equal(TipoAviso, item.GetProperty("tipoMensaje").GetString());
        Assert.Equal("avisos-comercio", item.GetProperty("colaOrigen").GetString());
        Assert.Equal("Fallido", item.GetProperty("estado").GetString());
        Assert.Contains("HTTP 500", item.GetProperty("motivoFallo").GetString());
        Assert.Equal(1, item.GetProperty("intentos").GetInt32());

        var detalle = await admin.GetAsync($"{Cola}/{id}");
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
    }

    [FactPostgres]
    public async Task LosFiltrosDeLaPantalla_FuncionanConElAvisoYConLosMensajesDeSiempre()
    {
        var idAviso = await CrearAvisoAgotadoAsync();
        var idMensaje = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            using var db = NuevoContexto(scope);
            db.MensajesFallidos.Add(new MensajeFallidoCola(idMensaje, "EventoPaqueteDespachado", "{\"x\":1}", "Falla de prueba", operadorId: DatabaseSeeder.DefaultOperadorId));
            await db.SaveChangesAsync();
        }

        var admin = await AdminAsync();

        // "Pendiente" es el vocabulario de la pantalla; la base guarda "Fallido". Antes ese filtro no devolvía nada
        var pendientes = await ListarAsync(admin, "?estado=Pendiente");
        Assert.True(Contiene(pendientes, idAviso));
        Assert.True(Contiene(pendientes, idMensaje));

        var soloAvisos = await ListarAsync(admin, $"?tipo={TipoAviso}");
        Assert.True(Contiene(soloAvisos, idAviso));
        Assert.False(Contiene(soloAvisos, idMensaje));

        var soloDespacho = await ListarAsync(admin, "?tipo=EventoPaqueteDespachado");
        Assert.False(Contiene(soloDespacho, idAviso));
        Assert.True(Contiene(soloDespacho, idMensaje));

        Assert.False(Contiene(await ListarAsync(admin, "?estado=Descartado"), idAviso));
        Assert.True(Contiene(await ListarAsync(admin, "?busqueda=HTTP%20500"), idAviso));
        Assert.False(Contiene(await ListarAsync(admin, "?busqueda=texto-que-no-existe-en-ningun-lado"), idAviso));
    }

    [FactPostgres]
    public async Task ElAvisoDeUnOperador_NoSeVeNiSeToca_DesdeLaColaDeOtroOperador()
    {
        var id = await CrearAvisoAgotadoAsync();
        var adminDos = await ClienteComoAsync("admin2@gmail.com", "Admin.2026!");

        Assert.False(Contiene(await ListarAsync(adminDos), id));
        Assert.Equal(HttpStatusCode.NotFound, (await adminDos.GetAsync($"{Cola}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adminDos.PostAsJsonAsync($"{Cola}/{id}/descartar", new { motivo = "intento ajeno" })).StatusCode);

        var reintento = await adminDos.PostAsJsonAsync($"{Cola}/reintentar", new { ids = new[] { id } });
        Assert.Equal(HttpStatusCode.OK, reintento.StatusCode);
        var resultado = await reintento.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, resultado.GetProperty("exitosos").GetInt32());
        Assert.Equal(0, resultado.GetProperty("fallidos").GetInt32());

        // Nada cambió para su dueño
        var aviso = await LeerAvisoAsync(id);
        Assert.Equal("Agotado", aviso.Estado);
        Assert.Equal(1, aviso.Intentos);
    }

    [FactPostgres]
    public async Task DescartarUnAviso_LoSacaDeLaColaYLoConservaConSuMotivo()
    {
        var id = await CrearAvisoAgotadoAsync();
        var admin = await AdminAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"{Cola}/{id}/descartar", new { motivo = "  " })).StatusCode);
        Assert.Equal("Agotado", (await LeerAvisoAsync(id)).Estado);

        var respuesta = await admin.PostAsJsonAsync($"{Cola}/{id}/descartar", new { motivo = "El comercio dio de baja su integración" });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var descartado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Descartado", descartado.GetProperty("estado").GetString());

        var aviso = await LeerAvisoAsync(id);
        Assert.Equal("Descartado", aviso.Estado);
        Assert.Contains("dio de baja su integración", aviso.UltimaRespuestaHttp);
        Assert.False(Contiene(await ListarAsync(admin), id));

        // Ya no está en la cola: descartarlo otra vez es "no encontrado", no un segundo descarte
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"{Cola}/{id}/descartar", new { motivo = "otra vez" })).StatusCode);
    }

    [FactPostgres]
    public async Task ReintentarUnAviso_LoReenviaDeVerdad_YSiElComercioSigueFallandoReiniciaLosReintentosAutomaticos()
    {
        var id = await CrearAvisoAgotadoAsync();
        var admin = await AdminAsync();

        var respuesta = await admin.PostAsJsonAsync($"{Cola}/reintentar", new { ids = new[] { id } });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, resultado.GetProperty("exitosos").GetInt32());
        Assert.Equal(1, resultado.GetProperty("fallidos").GetInt32());
        Assert.Contains(resultado.GetProperty("detalles").EnumerateArray(), d => d.GetString()!.StartsWith("[Fallo]"));

        // Se reenvió (no se reencoló el evento): un intento nuevo, de vuelta en el ciclo de reintentos con espera creciente
        var aviso = await LeerAvisoAsync(id);
        Assert.Equal("Reintentando", aviso.Estado);
        Assert.Equal(1, aviso.Intentos);
        Assert.NotNull(aviso.ProximoReintento);
        Assert.False(Contiene(await ListarAsync(admin), id));
    }

    [FactPostgres]
    public async Task ElPingDelPortal_HaceElEnvioDeVerdad_YNoInventaUnExito()
    {
        // Antes devolvía siempre "HTTP 200 OK, el receptor validó la firma" sin enviar nada
        var comercio = await ClienteComoAsync("comercio@gmail.com", "Comercio.2026!");

        var respuesta = await comercio.PostAsJsonAsync("/api/comercio/avisos/probar", new { urlWebhook = "http://127.0.0.1:9/hook" });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(resultado.GetProperty("exitoso").GetBoolean());
        Assert.Contains("interna", resultado.GetProperty("mensaje").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("sha256=", resultado.GetProperty("firmaGenerada").GetString());
    }

    [FactPostgres]
    public async Task ElComercio_NoPuedeVerLaColaDeFallidosDelOperador()
    {
        var comercio = await ClienteComoAsync("comercio@gmail.com", "Comercio.2026!");

        Assert.Equal(HttpStatusCode.Forbidden, (await comercio.GetAsync(Cola + "/")).StatusCode);
    }
}
