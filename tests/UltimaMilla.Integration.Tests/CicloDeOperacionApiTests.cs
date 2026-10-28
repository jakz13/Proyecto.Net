using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Frontend.Contracts.Modelos.Reportes;
using UltimaMilla.Frontend.Infrastructure.Http;
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

        // Tablero en vivo: la ruta despachada aparece con la posición que informa la app
        await Json(await cliente.PostAsJsonAsync("/api/mobile/v1/posiciones", new
        {
            posiciones = new[] { new { latitud = -34.9, longitud = -56.16, velocidadKmh = 20.0, bateriaPct = 80, hojaDeRutaId = rutaId } }
        }), HttpStatusCode.OK);

        var tablero = new HttpTableroApi(cliente);
        var resumen = await tablero.ObtenerResumenEnVivoAsync();
        var enVivo = Assert.Single(resumen.Vehiculos, v => v.HojaDeRutaId == rutaId);
        Assert.Equal(-34.9, enVivo.Latitud, 3);
        Assert.Equal(80, enVivo.NivelBateriaPct);
        Assert.Equal(1, enVivo.TotalParadas);
        Assert.Equal(codigo, Assert.Single(enVivo.Paradas).CodigoSeguimiento);
        Assert.True(resumen.TotalEnviosEnTransito >= 1);
        Assert.Equal(rutaId, (await tablero.ObtenerDetalleVehiculoEnVivoAsync(rutaId))!.HojaDeRutaId);
        Assert.Null(await tablero.ObtenerDetalleVehiculoEnVivoAsync(Guid.NewGuid()));
        Assert.False(await tablero.EnviarInstruccionRepartidorAsync(Guid.NewGuid(), "Hola"));
        Assert.True(await tablero.EnviarInstruccionRepartidorAsync(enVivo.RepartidorId, "Pasá por el depósito"));

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

        // Reportes de gestión con el cliente real del backoffice: el envío entregado aparece en cada reporte
        var reportes = new HttpReportesApi(cliente);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        FiltroReporteDto Periodo(string tipo) => new() { TipoReporte = tipo, FechaDesde = hoy.AddDays(-1), FechaHasta = hoy.AddDays(1) };

        foreach (var tipo in new[] { "CumplimientoZona", "CumplimientoRepartidor", "CumplimientoComercio" })
        {
            var cumplimiento = await reportes.GenerarReporteAsync(Periodo(tipo));
            Assert.True(cumplimiento.TotalEntregados >= 1, $"{tipo}: debería contar la entrega");
            Assert.All(cumplimiento.ItemsCumplimiento, i => Assert.True(i.EntregadosEnPlazo + i.EntregadosConDemora + i.NoEntregados <= i.TotalAsignados));
        }

        var volumen = await reportes.GenerarReporteAsync(Periodo("VolumenPeriodo"));
        Assert.True(volumen.ItemsVolumen.Sum(v => v.CantidadGuias) >= 1);
        Assert.True(volumen.ItemsVolumen.Sum(v => v.PesoTotalKg) > 0);

        var motivos = await reportes.GenerarReporteAsync(Periodo("MotivosNoEntrega"));
        Assert.All(motivos.ItemsMotivos, m => Assert.True(m.CantidadOcurrencias > 0));

        var liquidacion = await reportes.GenerarReporteAsync(Periodo("LiquidacionComercio"));
        Assert.NotEmpty(liquidacion.ItemsLiquidacion);
        Assert.True(liquidacion.TotalFacturadoLiquidacion > 0);

        var csv = await reportes.ExportarReporteCsvAsync(Periodo("LiquidacionComercio"));
        Assert.Contains("Total Facturado", System.Text.Encoding.UTF8.GetString(csv));

        await Assert.ThrowsAsync<UltimaMilla.Frontend.Infrastructure.Excepciones.ApiExcepcion>(() => reportes.GenerarReporteAsync(Periodo("Inexistente")));

        // Cierre del período: se liquida lo entregado una sola vez y queda en la cuenta corriente del comercio
        var comercioOperadorId = liquidacion.ItemsLiquidacion.First().ComercioId;
        var generacion = await Json(await cliente.PostAsJsonAsync("/api/liquidaciones/generar",
            new { desde = hoy.AddDays(-1), hasta = hoy.AddDays(1), comercioOperadorId }), HttpStatusCode.OK);
        Assert.True(generacion.GetProperty("enviosLiquidados").GetInt32() >= 1);
        var liquidacionCreada = generacion.GetProperty("liquidaciones")[0];
        var liquidacionId = liquidacionCreada.GetProperty("id").GetGuid();
        Assert.Equal("Pendiente", liquidacionCreada.GetProperty("estado").GetString());
        Assert.Equal(
            liquidacionCreada.GetProperty("subtotal").GetDecimal() + liquidacionCreada.GetProperty("iva").GetDecimal(),
            liquidacionCreada.GetProperty("montoTotal").GetDecimal());

        var repetida = await Json(await cliente.PostAsJsonAsync("/api/liquidaciones/generar",
            new { desde = hoy.AddDays(-1), hasta = hoy.AddDays(1), comercioOperadorId }), HttpStatusCode.OK);
        Assert.Equal(0, repetida.GetProperty("enviosLiquidados").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsync($"/api/liquidaciones/{liquidacionId}/pagar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PostAsync($"/api/liquidaciones/{liquidacionId}/aprobar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsync($"/api/liquidaciones/{liquidacionId}/aprobar", null)).StatusCode);

        var listadas = await Json(await cliente.GetAsync($"/api/liquidaciones?comercioOperadorId={comercioOperadorId}&estado=Aprobada"), HttpStatusCode.OK);
        Assert.Contains(listadas.EnumerateArray(), l => l.GetProperty("id").GetGuid() == liquidacionId);

        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PostAsync($"/api/liquidaciones/{liquidacionId}/pagar", null)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var movimientos = await db.MovimientosCuentaCorriente.AsNoTracking().Where(m => m.LiquidacionId == liquidacionId).ToListAsync();
            Assert.Equal(liquidacionCreada.GetProperty("montoTotal").GetDecimal(), movimientos.Where(m => m.Monto > 0).Sum(m => m.Monto));
            Assert.Equal(0m, movimientos.Sum(m => m.Monto)); // el pago compensa exactamente los cargos de esta liquidación
        }

        // Lo que ve el propio comercio en el portal: saldo en cero tras el pago y la liquidación pagada
        var cuenta = await Json(await cliente.GetAsync("/api/comercio/cuenta-corriente"), HttpStatusCode.OK);
        Assert.Equal(0m, cuenta.GetProperty("saldo").GetDecimal());
        Assert.True(cuenta.GetProperty("movimientos").GetArrayLength() >= 3);

        var delComercio = await Json(await cliente.GetAsync("/api/comercio/liquidaciones"), HttpStatusCode.OK);
        var propia = Assert.Single(delComercio.EnumerateArray(), l => l.GetProperty("id").GetGuid() == liquidacionId);
        Assert.Equal("Pagada", propia.GetProperty("estado").GetString());
    }

    private static string EstadoComoTexto(JsonElement estado) =>
        estado.ValueKind == JsonValueKind.String
            ? estado.GetString()!
            : ((UltimaMilla.Domain.Enums.EstadoEnvio)estado.GetInt32()).ToString();
}
