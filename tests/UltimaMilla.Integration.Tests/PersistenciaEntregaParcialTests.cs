using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// La entrega parcial debe sobrevivir a un reinicio y verse igual desde cualquier instancia de la API y desde el worker.
/// Estas pruebas guardan en Postgres real y vuelven a leer con un contexto nuevo, de modo que un dato mantenido solo en memoria falla.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class PersistenciaEntregaParcialTests
{
    private readonly ApiFactory _factory;

    public PersistenciaEntregaParcialTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private AppDbContext NuevoContexto(IServiceScope scope)
    {
        _ = _factory.CreateClient(); // asegura que el host arrancó y aplicó migraciones y semillas
        var opciones = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
        return new AppDbContext(opciones, null);
    }

    [FactPostgres]
    public async Task ReglaOperativa_ConservaLaPoliticaDeEntregaParcialConfigurada()
    {
        var id = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        using (var db = NuevoContexto(scope))
        {
            // Vigencia ya vencida: no interfiere con la regla vigente que usan los demás tests
            var regla = new ReglaOperativa(
                id, DatabaseSeeder.DefaultOperadorId, version: 9000 + Random.Shared.Next(1, 900),
                vigenteDesde: new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                maxIntentosEntrega: 2, plazoEntreIntentos: TimeSpan.FromHours(12),
                tipoPruebaEntregaExigida: "Firma", politicaDevolucion: "Retorno a depósito",
                plazoComprometidoEstandar: TimeSpan.FromHours(48), plazoComprometidoUrgente: TimeSpan.FromHours(24),
                vigenteHasta: new DateTime(2000, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                admiteEntregaParcial: false,
                politicaEntregaParcial: ReglaOperativa.PoliticaDevolucionAlComercio);
            db.ReglasOperativas.Add(regla);
            using var _ = db.HabilitarEscrituraAdministrativa();
            await db.SaveChangesAsync();
        }

        using var scope2 = _factory.Services.CreateScope();
        using var db2 = NuevoContexto(scope2);
        var leida = await db2.ReglasOperativas.IgnoreQueryFilters().AsNoTracking().SingleAsync(r => r.Id == id);

        Assert.False(leida.AdmiteEntregaParcial);
        Assert.Equal(ReglaOperativa.PoliticaDevolucionAlComercio, leida.PoliticaEntregaParcial);
    }

    [FactPostgres]
    public async Task SuscripcionAviso_ConservaLaVersionDeContratoV2()
    {
        var id = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        using (var db = NuevoContexto(scope))
        {
            db.SuscripcionesAviso.Add(new SuscripcionAviso(
                id, DatabaseSeeder.DefaultComercioOperadorId, "EnvioCambioEstado", "https://receptor.invalid/aviso", "clave-de-prueba",
                SuscripcionAviso.ContratoV2));
            using var _ = db.HabilitarEscrituraAdministrativa();
            await db.SaveChangesAsync();
        }

        using var scope2 = _factory.Services.CreateScope();
        using var db2 = NuevoContexto(scope2);
        var leida = await db2.SuscripcionesAviso.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == id);

        Assert.Equal(SuscripcionAviso.ContratoV2, leida.VersionContrato);
    }

    [FactPostgres]
    public async Task Bulto_ConservaElMotivoDeNoEntregaYLaPruebaDeEntregaAsociada()
    {
        var idNoEntregado = Guid.NewGuid();
        var idEntregado = Guid.NewGuid();
        var sufijo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var pruebaId = Guid.Empty;

        using (var scope = _factory.Services.CreateScope())
        using (var db = NuevoContexto(scope))
        {
            var noEntregado = new Bulto(idNoEntregado, DatabaseSeeder.Envio1Id, $"PERSIST-{sufijo}-1", 1, 10, 10, 10);
            noEntregado.MarcarRecibido();
            noEntregado.MarcarCargadoEnVehiculo();
            noEntregado.MarcarNoEntregado("AUSENTE");

            var entregado = new Bulto(idEntregado, DatabaseSeeder.Envio1Id, $"PERSIST-{sufijo}-2", 1, 10, 10, 10);
            entregado.MarcarRecibido();
            entregado.MarcarCargadoEnVehiculo();

            var intento = new IntentoEntrega(Guid.NewGuid(), DatabaseSeeder.Parada1Id, DatabaseSeeder.Envio1Id,
                resultado: "Exitoso", origenDatos: "MobileApp", motivoNoEntregaId: null, latitud: -34.9, longitud: -56.1);
            var prueba = new PruebaDeEntrega(intento.Id, "Firma", nombreReceptor: "Receptor", documentoReceptor: "12345678",
                codigosBultosEntregados: new[] { $"PERSIST-{sufijo}-2" });
            intento.AdjuntarPruebaEntrega(prueba);
            pruebaId = prueba.Id;
            entregado.MarcarEntregado(prueba.Id);

            db.Bultos.AddRange(noEntregado, entregado);
            db.IntentosEntrega.Add(intento);
            using var _ = db.HabilitarEscrituraAdministrativa();
            await db.SaveChangesAsync();
        }

        using var scope2 = _factory.Services.CreateScope();
        using var db2 = NuevoContexto(scope2);
        var bNoEntregado = await db2.Bultos.IgnoreQueryFilters().AsNoTracking().SingleAsync(b => b.Id == idNoEntregado);
        var bEntregado = await db2.Bultos.IgnoreQueryFilters().AsNoTracking().SingleAsync(b => b.Id == idEntregado);
        var pruebaLeida = await db2.PruebasDeEntrega.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == pruebaId);

        Assert.Equal("AUSENTE", bNoEntregado.MotivoNoEntregaCodigo);
        Assert.Null(bNoEntregado.PruebaDeEntregaId);
        Assert.Equal(pruebaId, bEntregado.PruebaDeEntregaId);
        Assert.Null(bEntregado.MotivoNoEntregaCodigo);
        Assert.Equal(new[] { $"PERSIST-{sufijo}-2" }, pruebaLeida.CodigosBultosEntregados);
    }

    [FactPostgres]
    public async Task CargaYEntregaParcial_ConFaltanteEnCargaYConFaltanteEnEnvio_PersisteAmbosComoEntregaParcialEnPostgres()
    {
        async Task<HttpClient> LoginAsync(string email, string password)
        {
            var c = _factory.CreateClient();
            var r = await c.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var token = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return c;
        }

        var sufijo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var admin = await LoginAsync("admin@gmail.com", "Admin.2026!");
        var comercio = await LoginAsync("comercio@gmail.com", "Comercio.2026!");
        var deposito = await LoginAsync("deposito@gmail.com", "Deposito.2026!");
        var despachador = await UsuariosDePrueba.DespachadorAsync(_factory);

        var repEmail = $"parcial.{sufijo.ToLowerInvariant()}@ultimamilla.com.uy";
        var repResp = await admin.PostAsJsonAsync("/api/v1/flota/repartidores", new
        {
            nombreCompleto = "Repartidor Parcial " + sufijo,
            email = repEmail,
            documento = CedulaDePrueba.Nueva(),
            telefono = "099444555",
            password = "Repartidor.2026!"
        });
        Assert.Equal(HttpStatusCode.Created, repResp.StatusCode);
        var repartidorId = (await repResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var vehResp = await admin.PostAsJsonAsync("/api/v1/flota/vehiculos", new
        {
            patente = "P" + sufijo[..7],
            tipo = "Furgón",
            capacidadPeso = 800,
            capacidadVolumen = 8
        });
        Assert.Equal(HttpStatusCode.Created, vehResp.StatusCode);
        var vehiculoId = (await vehResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var repartidor = await LoginAsync(repEmail, "Repartidor.2026!");

        async Task<(Guid EnvioId, string B1, string B2)> CrearEnvioDosBultosAsync(string destSuffix)
        {
            var alta = await comercio.PostAsJsonAsync("/api/envios", new
            {
                modalidadServicio = "Estandar",
                nombreDestinatario = "Cliente " + destSuffix,
                calle = "Av. 18 de Julio",
                numero = "1800",
                ciudad = "Montevideo",
                bultos = new[]
                {
                    new { peso = 2, alto = 10, ancho = 10, profundo = 10 },
                    new { peso = 3, alto = 12, ancho = 12, profundo = 12 }
                }
            });
            Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
            var envJson = await alta.Content.ReadFromJsonAsync<JsonElement>();
            var eid = envJson.GetProperty("id").GetGuid();
            var b1 = envJson.GetProperty("bultos")[0].GetString()!;
            var b2 = envJson.GetProperty("bultos")[1].GetString()!;

            Assert.Equal(HttpStatusCode.OK, (await deposito.PostAsJsonAsync($"/api/deposito/envios/{eid}/recepcion",
                new { codigosBultosEscaneados = new[] { b1, b2 } })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await deposito.PostAsJsonAsync($"/api/deposito/envios/{eid}/clasificacion",
                new
                {
                    bultos = new[]
                    {
                        new { codigoBulto = b1, prioridad = "Normal" },
                        new { codigoBulto = b2, prioridad = "Normal" }
                    }
                })).StatusCode);
            return (eid, b1, b2);
        }

        // Caso A: envío 1 perderá 1 bulto ya en la carga; Caso B: envío 2 sube completo y pierde 1 bulto en el envío
        var (envioAId, aB1, aB2) = await CrearEnvioDosBultosAsync("CasoA-" + sufijo);
        var (envioBId, bB1, bB2) = await CrearEnvioDosBultosAsync("CasoB-" + sufijo);

        var armada = await despachador.PostAsJsonAsync("/api/planificacion/hojas-de-ruta", new
        {
            repartidorId,
            vehiculoId,
            fecha = DateOnly.FromDateTime(DateTime.UtcNow),
            enviosIds = new[] { envioAId, envioBId },
            retirosIds = Array.Empty<Guid>()
        });
        Assert.Equal(HttpStatusCode.Created, armada.StatusCode);
        var rutaId = (await armada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("hojaDeRutaId").GetGuid();

        // En carga se escanea solo aB1 de Caso A (aB2 falta desde carga) y los 2 bultos de Caso B
        var cargaResp = await repartidor.PostAsJsonAsync($"/api/mobile/v1/rutas/{rutaId}/carga/confirmar", new
        {
            codigosBultosCargados = new[] { aB1, bB1, bB2 },
            idempotencyKey = $"CARGA-{sufijo}",
            observacionesDiscrepancia = "Falta segundo bulto del primer envío en andén"
        });
        Assert.Equal(HttpStatusCode.OK, cargaResp.StatusCode);
        var cargaJson = await cargaResp.Content.ReadFromJsonAsync<JsonElement>();
        // Confirmar la carga desde la app deja la hoja "En carga": no despacha. La salida la confirma el despachador/operario.
        Assert.Equal("EnCarga", cargaJson.GetProperty("estadoRuta").GetString());
        Assert.Equal(0, cargaJson.GetProperty("enviosEnTransito").GetInt32());

        var despacho = await despachador.PostAsJsonAsync($"/api/planificacion/hojas-de-ruta/{rutaId}/despacho", new { });
        Assert.Equal(HttpStatusCode.OK, despacho.StatusCode);
        var despachoJson = await despacho.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, despachoJson.GetProperty("enviosEnTransito").GetInt32());

        // La consulta móvil activa NO debe incluir aB2 (ya perdido en carga) en codigosBultos y sí informar el conteo faltante
        var activaResp = await repartidor.GetAsync("/api/mobile/v1/rutas/activa");
        Assert.Equal(HttpStatusCode.OK, activaResp.StatusCode);
        var activaJson = await activaResp.Content.ReadFromJsonAsync<JsonElement>();
        var motivos = activaJson.GetProperty("motivosNoEntrega");
        var motivoCodigo = motivos[0].GetProperty("codigo").GetString()!;

        var paradasJson = activaJson.GetProperty("paradas").EnumerateArray().ToList();
        var paradaA = paradasJson.Single(p => p.GetProperty("envioId").GetGuid() == envioAId);
        var paradaB = paradasJson.Single(p => p.GetProperty("envioId").GetGuid() == envioBId);

        var codigosA = paradaA.GetProperty("resumenBultos").GetProperty("codigosBultos").EnumerateArray().Select(x => x.GetString()!).ToList();
        Assert.Equal(new[] { aB1 }, codigosA);
        Assert.Equal(1, paradaA.GetProperty("resumenBultos").GetProperty("cantidadBultosFaltantesEnCarga").GetInt32());
        Assert.DoesNotContain(aB2, codigosA);

        var codigosB = paradaB.GetProperty("resumenBultos").GetProperty("codigosBultos").EnumerateArray().Select(x => x.GetString()!).ToList();
        Assert.Equal(2, codigosB.Count);
        Assert.Contains(bB1, codigosB);
        Assert.Contains(bB2, codigosB);

        // Entrega Parcial Caso A: solo se entrega aB1 (único bulto visible cargado), sin bB2 ni motivo de puerta
        var paradaAId = paradaA.GetProperty("paradaId").GetGuid();
        var respEntregaA = await repartidor.PostAsJsonAsync(
            $"/api/mobile/v1/rutas/{rutaId}/paradas/{paradaAId}/entrega-parcial",
            new
            {
                envioId = envioAId,
                idempotencyKey = $"PARC-A-{sufijo}",
                codigosBultosEntregados = new[] { aB1 },
                bultosNoEntregados = Array.Empty<object>(),
                nombreReceptor = "Receptor Caso A",
                documentoReceptor = "41234567",
                firmaDigitalBase64 = "FIRMA_CASE_A",
                fotoEvidenciaBase64 = "FOTO_CASE_A",
                latitud = -34.9011,
                longitud = -56.1645,
                fechaHoraDispositivoUtc = DateTime.UtcNow
            });
        Assert.Equal(HttpStatusCode.OK, respEntregaA.StatusCode);
        var resJsonA = await respEntregaA.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("EntregadoParcialmente", resJsonA.GetProperty("estadoEnvio").GetString());
        Assert.Equal("EntregadaParcialmente", resJsonA.GetProperty("estadoParada").GetString());

        // Entrega Parcial Caso B: entrega bB1 y bB2 se perdió en el reparto (con motivo)
        var paradaBId = paradaB.GetProperty("paradaId").GetGuid();
        var respEntregaB = await repartidor.PostAsJsonAsync(
            $"/api/mobile/v1/rutas/{rutaId}/paradas/{paradaBId}/entrega-parcial",
            new
            {
                envioId = envioBId,
                idempotencyKey = $"PARC-B-{sufijo}",
                codigosBultosEntregados = new[] { bB1 },
                bultosNoEntregados = new[]
                {
                    new { codigoBulto = bB2, motivoCodigo, observaciones = "Bulto extraviado durante el reparto" }
                },
                nombreReceptor = "Receptor Caso B",
                documentoReceptor = "42345678",
                firmaDigitalBase64 = "FIRMA_CASE_B",
                fotoEvidenciaBase64 = "FOTO_CASE_B",
                latitud = -34.9022,
                longitud = -56.1655,
                fechaHoraDispositivoUtc = DateTime.UtcNow
            });
        Assert.Equal(HttpStatusCode.OK, respEntregaB.StatusCode);
        var resJsonB = await respEntregaB.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("EntregadoParcialmente", resJsonB.GetProperty("estadoEnvio").GetString());
        Assert.Equal("EntregadaParcialmente", resJsonB.GetProperty("estadoParada").GetString());

        // Verificación persistida en PostgreSQL con contexto nuevo
        using var scope = _factory.Services.CreateScope();
        using var db = NuevoContexto(scope);
        var envioA = await db.Envios.Include(e => e.Bultos).IgnoreQueryFilters().AsNoTracking().SingleAsync(e => e.Id == envioAId);
        var envioB = await db.Envios.Include(e => e.Bultos).IgnoreQueryFilters().AsNoTracking().SingleAsync(e => e.Id == envioBId);

        Assert.Equal(Domain.Enums.EstadoEnvio.EntregadoParcialmente, envioA.EstadoActual);
        Assert.Equal("Entregado", envioA.Bultos.Single(b => b.Identificacion == aB1).EstadoBulto);
        Assert.Equal("FaltanteEnCarga", envioA.Bultos.Single(b => b.Identificacion == aB2).EstadoBulto);

        Assert.Equal(Domain.Enums.EstadoEnvio.EntregadoParcialmente, envioB.EstadoActual);
        Assert.Equal("Entregado", envioB.Bultos.Single(b => b.Identificacion == bB1).EstadoBulto);
        Assert.NotEqual("Entregado", envioB.Bultos.Single(b => b.Identificacion == bB2).EstadoBulto);
    }
}
