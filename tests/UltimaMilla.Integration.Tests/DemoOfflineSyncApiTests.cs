using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Valida el ciclo completo de sincronización de operaciones realizadas en modo offline (UC-20, UC-21, Geocercas 7.3)
/// y la capacidad de reinicio de la demo (/api/mobile/v1/demo/reset) contra PostgreSQL real.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class DemoOfflineSyncApiTests
{
    private readonly ApiFactory _factory;

    public DemoOfflineSyncApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage respuesta, HttpStatusCode esperado)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == esperado, $"Se esperaba {(int)esperado} y llegó {(int)respuesta.StatusCode}: {cuerpo}");
        return string.IsNullOrWhiteSpace(cuerpo) ? default : JsonSerializer.Deserialize<JsonElement>(cuerpo);
    }

    [FactPostgres]
    public async Task DemoReset_RestauraEstadoInicial_TodosLosEnviosEnTransito()
    {
        var cliente = _factory.CreateClient();

        // 1. Invocar el endpoint de reset
        var resetRes = await cliente.PostAsync("/api/mobile/v1/demo/reset", null);
        var jsonRes = await Json(resetRes, HttpStatusCode.OK);

        Assert.True(jsonRes.TryGetProperty("enviosReiniciados", out var enviosArray));

        // 2. Verificar estado en PostgreSQL
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hojaDeRuta = await db.HojasDeRuta
            .Include(h => h.Paradas)
            .SingleAsync(h => h.Id == DatabaseSeeder.DefaultHojaDeRutaId);

        Assert.Equal("Despachada", hojaDeRuta.Estado);
        Assert.All(hojaDeRuta.Paradas, p => Assert.Equal("Pendiente", p.Estado));

        var envios = await db.Envios
            .Include(e => e.IntentosEntrega)
            .Where(e => e.Id == DatabaseSeeder.Envio1Id || e.Id == DatabaseSeeder.Envio2Id || e.Id == DatabaseSeeder.Envio3Id)
            .ToListAsync();

        Assert.Equal(3, envios.Count);
        Assert.All(envios, e =>
        {
            Assert.Equal(EstadoEnvio.EnTransito, e.EstadoActual);
            Assert.Empty(e.IntentosEntrega);
        });
    }

    [FactPostgres]
    public async Task FlujoOfflineOnline_SincronizacionYReset_ActualizaPostgreSQLYPermiteReinicio()
    {
        var cliente = _factory.CreateClient();

        // Asegurar estado inicial limpio
        var resetInicial = await cliente.PostAsync("/api/mobile/v1/demo/reset", null);
        await Json(resetInicial, HttpStatusCode.OK);

        // =========================================================================
        // 1. Sincronizar Operación Offline 1: Entrega Exitosa de Parada 2 (ENV-2026-00109)
        // =========================================================================
        var entregaPayload = new
        {
            envioId = DatabaseSeeder.Envio2Id,
            idempotencyKey = "sync-test-offline-00109-" + Guid.NewGuid(),
            nombreReceptor = "Lucía Fernández",
            documentoReceptor = "4.567.890-1",
            firmaDigitalBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
            fotoEvidenciaBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
        };

        var respuestaEntrega = await cliente.PostAsJsonAsync(
            $"/api/mobile/v1/rutas/{DatabaseSeeder.DefaultHojaDeRutaId}/paradas/{DatabaseSeeder.Parada2Id}/entrega-exitosa",
            entregaPayload);
        await Json(respuestaEntrega, HttpStatusCode.OK);

        // Verificar que en PostgreSQL ENV-2026-00109 pasó a Entregado (5)
        var seguimiento00109 = await Json(await cliente.GetAsync("/api/seguimiento/ENV-2026-00109"), HttpStatusCode.OK);
        var estado00109 = seguimiento00109.GetProperty("estadoActual");
        var estado00109Texto = estado00109.ValueKind == JsonValueKind.String
            ? estado00109.GetString()
            : ((EstadoEnvio)estado00109.GetInt32()).ToString();
        Assert.Equal("Entregado", estado00109Texto);

        // =========================================================================
        // 2. Sincronizar Operación Offline 2: Intento Fallido de Parada 3 (ENV-2026-00110)
        // =========================================================================
        var intentoPayload = new
        {
            envioId = DatabaseSeeder.Envio3Id,
            idempotencyKey = "sync-test-offline-00110-" + Guid.NewGuid(),
            motivoCodigo = "CLI_AUSENTE",
            observaciones = "No responde al timbre en segunda visita offline",
            fotoEvidenciaBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
        };

        var respuestaIntento = await cliente.PostAsJsonAsync(
            $"/api/mobile/v1/rutas/{DatabaseSeeder.DefaultHojaDeRutaId}/paradas/{DatabaseSeeder.Parada3Id}/intento-fallido",
            intentoPayload);
        await Json(respuestaIntento, HttpStatusCode.OK);

        // Verificar en BD que el intento fue registrado correctamente con motivo CLI_AUSENTE
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var envio3 = await db.Envios
                .Include(e => e.IntentosEntrega)
                .SingleAsync(e => e.Id == DatabaseSeeder.Envio3Id);

            Assert.NotEmpty(envio3.IntentosEntrega);
            var intento = envio3.IntentosEntrega.Last();
            Assert.Equal("Fallido", intento.Resultado);

            var parada3 = await db.Paradas.SingleAsync(p => p.Id == DatabaseSeeder.Parada3Id);
            Assert.Equal("Fallida", parada3.Estado);
        }

        // =========================================================================
        // 3. Reiniciar Demo y comprobar restauración completa
        // =========================================================================
        var resetFinal = await cliente.PostAsync("/api/mobile/v1/demo/reset", null);
        await Json(resetFinal, HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var envio2 = await db.Envios.Include(e => e.IntentosEntrega).SingleAsync(e => e.Id == DatabaseSeeder.Envio2Id);
            var envio3 = await db.Envios.Include(e => e.IntentosEntrega).SingleAsync(e => e.Id == DatabaseSeeder.Envio3Id);

            Assert.Equal(EstadoEnvio.EnTransito, envio2.EstadoActual);
            Assert.Equal(EstadoEnvio.EnTransito, envio3.EstadoActual);
            Assert.Empty(envio2.IntentosEntrega);
            Assert.Empty(envio3.IntentosEntrega);

            var parada2 = await db.Paradas.SingleAsync(p => p.Id == DatabaseSeeder.Parada2Id);
            var parada3 = await db.Paradas.SingleAsync(p => p.Id == DatabaseSeeder.Parada3Id);
            Assert.Equal("Pendiente", parada2.Estado);
            Assert.Equal("Pendiente", parada3.Estado);
        }
    }

    [FactPostgres]
    public async Task Geocercas_LlegadaYSalida_RegistraEventosEnSeguimientoPublicoYDemoResetLosLimpia()
    {
        var cliente = _factory.CreateClient();

        // 1. Asegurar estado inicial limpio
        await Json(await cliente.PostAsync("/api/mobile/v1/demo/reset", null), HttpStatusCode.OK);

        // 2. Registrar LLEGADA_GEOCERCA (< 100m, ej. 45m) en Parada 1 (ENV-2026-00108)
        var llegadaPayload = new
        {
            envioId = DatabaseSeeder.Envio1Id,
            tipoEvento = "LLEGADA_GEOCERCA",
            latitud = -34.905395,
            longitud = -56.1885,
            distanciaMetros = 45.0,
            idempotencyKey = "geo-in-" + Guid.NewGuid()
        };

        var resLlegada = await cliente.PostAsJsonAsync(
            $"/api/mobile/v1/rutas/{DatabaseSeeder.DefaultHojaDeRutaId}/paradas/{DatabaseSeeder.Parada1Id}/geocerca",
            llegadaPayload);
        var jsonLlegada = await Json(resLlegada, HttpStatusCode.OK);
        Assert.Equal("LlegadaGeocerca", jsonLlegada.GetProperty("tipoEvento").GetString());
        Assert.False(jsonLlegada.GetProperty("idempotente").GetBoolean());

        // Verificar idempotencia ante reintento consecutivo de LLEGADA_GEOCERCA
        var resLlegadaDuplicada = await cliente.PostAsJsonAsync(
            $"/api/mobile/v1/rutas/{DatabaseSeeder.DefaultHojaDeRutaId}/paradas/{DatabaseSeeder.Parada1Id}/geocerca",
            llegadaPayload);
        var jsonLlegadaDuplicada = await Json(resLlegadaDuplicada, HttpStatusCode.OK);
        Assert.True(jsonLlegadaDuplicada.GetProperty("idempotente").GetBoolean());

        // 3. Registrar SALIDA_GEOCERCA (> 150m, ej. 220m) en Parada 1 (ENV-2026-00108)
        var salidaPayload = new
        {
            envioId = DatabaseSeeder.Envio1Id,
            tipoEvento = "SALIDA_GEOCERCA",
            latitud = -34.903820,
            longitud = -56.1885,
            distanciaMetros = 220.0,
            tiempoPermanenciaSegundos = 95,
            idempotencyKey = "geo-out-" + Guid.NewGuid()
        };

        var resSalida = await cliente.PostAsJsonAsync(
            $"/api/mobile/v1/rutas/{DatabaseSeeder.DefaultHojaDeRutaId}/paradas/{DatabaseSeeder.Parada1Id}/geocerca",
            salidaPayload);
        var jsonSalida = await Json(resSalida, HttpStatusCode.OK);
        Assert.Equal("SalidaGeocerca", jsonSalida.GetProperty("tipoEvento").GetString());
        Assert.False(jsonSalida.GetProperty("idempotente").GetBoolean());

        // 4. Comprobar que GET /api/seguimiento/ENV-2026-00108 refleja ambos eventos públicos (caché invalidada)
        var seguimiento00108 = await Json(await cliente.GetAsync("/api/seguimiento/ENV-2026-00108"), HttpStatusCode.OK);
        var hitos = seguimiento00108.GetProperty("historialEventos").EnumerateArray()
            .Select(h => h.GetProperty("descripcionPublica").GetString() ?? string.Empty)
            .ToList();

        Assert.Contains(hitos, d => d.Contains("Repartidor ingresó a la zona de entrega", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hitos, d => d.Contains("Repartidor salió de la zona de entrega (Permanencia: 95s)", StringComparison.OrdinalIgnoreCase));

        // 5. Ejecutar /api/mobile/v1/demo/reset y verificar que se limpien los eventos de geocerca
        await Json(await cliente.PostAsync("/api/mobile/v1/demo/reset", null), HttpStatusCode.OK);

        var seguimientoTrasReset = await Json(await cliente.GetAsync("/api/seguimiento/ENV-2026-00108"), HttpStatusCode.OK);
        var hitosTrasReset = seguimientoTrasReset.GetProperty("historialEventos").EnumerateArray()
            .Select(h => h.GetProperty("descripcionPublica").GetString() ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(hitosTrasReset, d => d.Contains("zona de entrega", StringComparison.OrdinalIgnoreCase));
    }
}
