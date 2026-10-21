using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Recorre de punta a punta la vida de un envío contra Postgres real:
/// alta, recepción, clasificación, armado, carga, despacho, entrega y rendición.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class CicloDeOperacionApiTests
{
    private static readonly Guid RutaSembrada = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");

    private readonly ApiFactory _factory;

    public CicloDeOperacionApiTests(ApiFactory factory)
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
    public async Task EnvioNuevo_RecorreTodoElCicloHastaSerEntregadoYRendido()
    {
        var cliente = _factory.CreateClient();

        // El repartidor de la demo tiene una ruta sin rendir: hasta rendirla no puede despachar otra
        List<string> bultosDeLaRutaSembrada;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            bultosDeLaRutaSembrada = await db.Envios.AsNoTracking()
                .Where(e => e.HojaDeRutaId == RutaSembrada)
                .SelectMany(e => e.Bultos.Select(b => b.Identificacion))
                .ToListAsync();
        }

        var rendicionSembrada = await cliente.PostAsJsonAsync($"/api/deposito/hojas-de-ruta/{RutaSembrada}/rendicion",
            new { codigosBultosDevueltos = bultosDeLaRutaSembrada, motivoInternoCodigo = "RETORNO_SIN_INTENTO" });
        await Json(rendicionSembrada, HttpStatusCode.OK);

        // Alta
        var alta = await Json(await cliente.PostAsJsonAsync("/api/envios", new
        {
            modalidadServicio = "Estandar",
            nombreDestinatario = "Ana Gomez",
            calle = "18 de Julio",
            numero = "1234",
            ciudad = "Montevideo",
            bultos = new[] { new { peso = 2, alto = 10, ancho = 10, profundo = 10 } }
        }), HttpStatusCode.Created);
        var envioId = alta.GetProperty("id").GetGuid();
        var codigo = alta.GetProperty("codigoSeguimiento").GetString()!;
        var bulto = alta.GetProperty("bultos")[0].GetString()!;

        // Recepción y clasificación en depósito
        var recepcion = await Json(await cliente.PostAsJsonAsync($"/api/deposito/envios/{envioId}/recepcion",
            new { codigosBultosEscaneados = new[] { bulto } }), HttpStatusCode.OK);
        Assert.True(recepcion.GetProperty("recepcionado").GetBoolean());

        var clasificacion = await Json(await cliente.PostAsJsonAsync($"/api/deposito/envios/{envioId}/clasificacion",
            new { bultos = new[] { new { codigoBulto = bulto, prioridad = "Normal" } } }), HttpStatusCode.OK);
        Assert.True(clasificacion.GetProperty("listoParaHojaDeRuta").GetBoolean());

        // Armado de la hoja de ruta
        var ruta = await Json(await cliente.PostAsJsonAsync("/api/planificacion/hojas-de-ruta", new
        {
            repartidorId = DatabaseSeeder.DefaultRepartidorId,
            vehiculoId = DatabaseSeeder.DefaultVehiculoId,
            fecha = DateOnly.FromDateTime(DateTime.UtcNow),
            enviosIds = new[] { envioId }
        }), HttpStatusCode.Created);
        var rutaId = ruta.GetProperty("hojaDeRutaId").GetGuid();

        // El mismo envío no puede entrar en una segunda ruta
        var segundaRuta = await cliente.PostAsJsonAsync("/api/planificacion/hojas-de-ruta", new
        {
            repartidorId = DatabaseSeeder.DefaultRepartidorId,
            vehiculoId = DatabaseSeeder.DefaultVehiculoId,
            fecha = DateOnly.FromDateTime(DateTime.UtcNow),
            enviosIds = new[] { envioId }
        });
        Assert.True(segundaRuta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            $"El envío ya asignado no debe poder armarse otra vez ({(int)segundaRuta.StatusCode})");

        // Despachar sin haber cargado el vehículo no es posible
        var despachoSinCarga = await cliente.PostAsJsonAsync($"/api/planificacion/hojas-de-ruta/{rutaId}/despacho", new { });
        Assert.Equal(HttpStatusCode.BadRequest, despachoSinCarga.StatusCode);

        // Carga del vehículo con la app y despacho
        await Json(await cliente.PostAsJsonAsync($"/api/mobile/v1/rutas/{rutaId}/carga/confirmar",
            new { codigosBultosCargados = new[] { bulto }, idempotencyKey = Guid.NewGuid().ToString() }), HttpStatusCode.OK);
        await Json(await cliente.PostAsJsonAsync($"/api/planificacion/hojas-de-ruta/{rutaId}/despacho", new { }), HttpStatusCode.OK);

        // Entrega en la parada
        Guid paradaId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            paradaId = await db.Paradas.AsNoTracking().Where(p => p.HojaDeRutaId == rutaId && p.EnvioId == envioId).Select(p => p.Id).SingleAsync();
        }

        var entrega = await cliente.PostAsJsonAsync($"/api/mobile/v1/rutas/{rutaId}/paradas/{paradaId}/entrega-exitosa", new
        {
            envioId,
            idempotencyKey = Guid.NewGuid().ToString(),
            nombreReceptor = "Ana Gomez",
            documentoReceptor = "1.234.567-8",
            firmaDigitalBase64 = "firma",
            fotoEvidenciaBase64 = "foto"
        });
        await Json(entrega, HttpStatusCode.OK);

        // Rendición de la ruta y verificación de lo que ve el destinatario
        await Json(await cliente.PostAsJsonAsync($"/api/deposito/hojas-de-ruta/{rutaId}/rendicion",
            new { codigosBultosDevueltos = Array.Empty<string>() }), HttpStatusCode.OK);

        var seguimiento = await Json(await cliente.GetAsync($"/api/seguimiento/{codigo}"), HttpStatusCode.OK);
        Assert.Equal("Entregado", EstadoComoTexto(seguimiento.GetProperty("estadoActual")));

        var publico = seguimiento.GetRawText();
        Assert.DoesNotContain("1.234.567-8", publico);
        Assert.DoesNotContain("18 de Julio", publico);
    }

    private static string EstadoComoTexto(JsonElement estado) =>
        estado.ValueKind == JsonValueKind.String
            ? estado.GetString()!
            : ((UltimaMilla.Domain.Enums.EstadoEnvio)estado.GetInt32()).ToString();
}
