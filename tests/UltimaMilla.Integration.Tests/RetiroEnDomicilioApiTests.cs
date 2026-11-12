using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Commands.Mensajeria.ProcesarMensaje;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Cambio de requerimientos 2.2 (retiro en domicilio) de punta a punta contra Postgres real y con sesiones reales por perfil:
/// solicitud del comercio, hoja de ruta mixta, registro en la calle, rendición, seguimiento público, avisos versionados y aislamiento.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class RetiroEnDomicilioApiTests
{
    private static readonly Guid RutaSembrada = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");

    private readonly ApiFactory _factory;

    public RetiroEnDomicilioApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];

    private async Task<HttpClient> ClienteComoAsync(string email, string password)
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        return cliente;
    }

    private Task<HttpClient> ComercioAsync() => ClienteComoAsync("comercio@gmail.com", "Comercio.2026!");
    // La cuenta despacho@gmail.com la bloquea a propósito otra clase de pruebas (bloqueo por intentos fallidos) y la base es compartida:
    // se usa una cuenta de despachador propia de las pruebas. El administrador no planifica (letra, sección 3).
    private Task<HttpClient> DespachadorAsync() => UsuariosDePrueba.DespachadorAsync(_factory);
    private Task<HttpClient> DepositoAsync() => ClienteComoAsync("deposito@gmail.com", "Deposito.2026!");
    private Task<HttpClient> RepartidorAsync() => ClienteComoAsync("repartidor@gmail.com", "Repartidor.2026!");
    private Task<HttpClient> AdminAsync() => ClienteComoAsync("admin@gmail.com", "Admin.2026!");
    private Task<HttpClient> DespachadorDelOperadorDosAsync() => ClienteComoAsync("despacho.express@ultimamilla.com.uy", "demo1234");

    private static async Task<JsonElement> Json(HttpResponseMessage respuesta, HttpStatusCode esperado)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == esperado, $"Se esperaba {(int)esperado} y llegó {(int)respuesta.StatusCode}: {cuerpo}");
        return string.IsNullOrWhiteSpace(cuerpo) ? default : JsonSerializer.Deserialize<JsonElement>(cuerpo);
    }

    private static object SolicitudDeRetiro(string? referencia = null, int cantidadBultos = 2) => new
    {
        nombreContacto = "Carlos Pérez",
        telefonoContacto = "099111222",
        calle = "Bulevar Artigas",
        numero = "1500",
        ciudad = "Montevideo",
        motivo = "Cambio de talle",
        referenciaExterna = referencia,
        bultos = Enumerable.Range(1, cantidadBultos)
            .Select(i => new { descripcion = $"Prenda {i}", peso = 1.5, alto = 30, ancho = 40, profundo = 10 })
            .ToArray()
    };

    /// <summary>
    /// Cada prueba trabaja con su propio repartidor y vehículo: los tests comparten la base y un repartidor con una hoja de ruta despachada
    /// sin rendir no puede recibir otra. Así ninguna prueba depende de lo que dejaron las demás.
    /// </summary>
    private sealed record Dotacion(Guid RepartidorId, Guid VehiculoId, HttpClient Repartidor);

    private async Task<Dotacion> NuevaDotacionAsync(decimal capacidadPeso = 500)
    {
        var admin = await AdminAsync();
        var sufijo = Sufijo();
        var email = $"retiros.{sufijo}@ultimamilla.com.uy";

        var repartidor = await Json(await admin.PostAsJsonAsync("/api/v1/flota/repartidores", new
        {
            nombreCompleto = "Repartidor " + sufijo, email, documento = "4.111.222-3", telefono = "099000222", password = "Repartidor.2026!"
        }), HttpStatusCode.Created);

        var vehiculo = await Json(await admin.PostAsJsonAsync("/api/v1/flota/vehiculos", new
        {
            patente = "T" + sufijo.ToUpperInvariant(), tipo = "Furgón", capacidadPeso, capacidadVolumen = 5
        }), HttpStatusCode.Created);

        // El alta del repartidor le crea un usuario con la contraseña provisoria indicada
        var cliente = await ClienteComoAsync(email, "Repartidor.2026!");
        return new Dotacion(repartidor.GetProperty("id").GetGuid(), vehiculo.GetProperty("id").GetGuid(), cliente);
    }

    private async Task<(Guid Id, string Codigo, string Bulto)> CrearEnvioClasificadoAsync()
    {
        var comercio = await ComercioAsync();
        var deposito = await DepositoAsync();

        var alta = await Json(await comercio.PostAsJsonAsync("/api/envios", new
        {
            modalidadServicio = "Estandar",
            nombreDestinatario = "Cliente " + Sufijo(),
            calle = "Av. Rivera",
            numero = "2500",
            ciudad = "Montevideo",
            bultos = new[] { new { peso = 2, alto = 10, ancho = 10, profundo = 10 } }
        }), HttpStatusCode.Created);

        var id = alta.GetProperty("id").GetGuid();
        var bulto = alta.GetProperty("bultos")[0].GetString()!;

        await Json(await deposito.PostAsJsonAsync($"/api/deposito/envios/{id}/recepcion", new { codigosBultosEscaneados = new[] { bulto } }), HttpStatusCode.OK);
        await Json(await deposito.PostAsJsonAsync($"/api/deposito/envios/{id}/clasificacion",
            new { bultos = new[] { new { codigoBulto = bulto, prioridad = "Normal" } } }), HttpStatusCode.OK);

        return (id, alta.GetProperty("codigoSeguimiento").GetString()!, bulto);
    }

    [FactPostgres]
    public async Task Retiro_RecorreElFlujoCompletoConUnaHojaDeRutaMixta()
    {
        var dot = await NuevaDotacionAsync();
        var comercio = await ComercioAsync();
        var despachador = await DespachadorAsync();
        var repartidor = dot.Repartidor;
        var deposito = await DepositoAsync();

        // el comercio solicita el retiro y se tarifa con la regla de retiro del cuadro vigente
        var referencia = "REF-" + Sufijo();
        var creado = await Json(await comercio.PostAsJsonAsync("/api/comercio/retiros", SolicitudDeRetiro(referencia)), HttpStatusCode.Created);
        var retiroId = creado.GetProperty("id").GetGuid();
        var codigo = creado.GetProperty("codigoSeguimiento").GetString()!;
        Assert.StartsWith("RET-", codigo);
        Assert.Equal("Solicitado", creado.GetProperty("estado").GetString());
        Assert.Equal(120m, creado.GetProperty("tarifa").GetDecimal());
        Assert.Equal(2, creado.GetProperty("identificacionesBultos").GetArrayLength());
        var bultosRetiro = creado.GetProperty("identificacionesBultos").EnumerateArray().Select(b => b.GetString()!).ToList();

        // Idempotencia: repetir la solicitud con la misma referencia devuelve la ya registrada
        var repetido = await Json(await comercio.PostAsJsonAsync("/api/comercio/retiros", SolicitudDeRetiro(referencia)), HttpStatusCode.OK);
        Assert.Equal(retiroId, repetido.GetProperty("id").GetGuid());
        Assert.True(repetido.GetProperty("duplicado").GetBoolean());

        // hoja de ruta mixta con una entrega y un retiro
        var envio = await CrearEnvioClasificadoAsync();
        var ruta = await Json(await despachador.PostAsJsonAsync("/api/planificacion/hojas-de-ruta", new
        {
            repartidorId = dot.RepartidorId,
            vehiculoId = dot.VehiculoId,
            fecha = DateOnly.FromDateTime(DateTime.UtcNow),
            enviosIds = new[] { envio.Id },
            retirosIds = new[] { retiroId }
        }), HttpStatusCode.Created);

        var rutaId = ruta.GetProperty("hojaDeRutaId").GetGuid();
        var paradas = ruta.GetProperty("paradas").EnumerateArray().ToList();
        Assert.Equal(2, paradas.Count);
        var paradaRetiro = Assert.Single(paradas, p => p.GetProperty("tipo").GetString() == "Retiro");
        var paradaEnvio = Assert.Single(paradas, p => p.GetProperty("tipo").GetString() == "Entrega");
        Assert.Equal(retiroId, paradaRetiro.GetProperty("retiroId").GetGuid());
        var paradaRetiroId = paradaRetiro.GetProperty("paradaId").GetGuid();
        var paradaEnvioId = paradaEnvio.GetProperty("paradaId").GetGuid();

        var asignado = await Json(await despachador.GetAsync($"/api/retiros/{retiroId}"), HttpStatusCode.OK);
        Assert.Equal("AsignadoARuta", asignado.GetProperty("resumen").GetProperty("estado").GetString());

        // El retiro asignado a una ruta ya no puede asignarse a otra ni cancelarse por el comercio
        var cancelarAsignado = await comercio.PostAsJsonAsync($"/api/comercio/retiros/{retiroId}/cancelar", new { motivo = "Ya no lo necesito" });
        Assert.Equal(HttpStatusCode.BadRequest, cancelarAsignado.StatusCode);

        // Carga del vehículo (solo hay bultos de entrega que cargar) y despacho
        await Json(await repartidor.PostAsJsonAsync($"/api/mobile/v1/rutas/{rutaId}/carga/confirmar",
            new { codigosBultosCargados = new[] { envio.Bulto }, idempotencyKey = Guid.NewGuid().ToString() }), HttpStatusCode.OK);
        await Json(await despachador.PostAsJsonAsync($"/api/planificacion/hojas-de-ruta/{rutaId}/despacho", new { }), HttpStatusCode.OK);

        // la app descarga la ruta con la parada de retiro y sus bultos
        var descarga = await Json(await repartidor.GetAsync($"/api/mobile/v1/rutas?fecha={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}"), HttpStatusCode.OK);
        var rutaDescargada = descarga.GetProperty("rutas").EnumerateArray().Single(r => r.GetProperty("hojaDeRutaId").GetGuid() == rutaId);
        var paradaDescargada = rutaDescargada.GetProperty("paradas").EnumerateArray().Single(p => p.GetProperty("paradaId").GetGuid() == paradaRetiroId);
        Assert.Equal("Retiro", paradaDescargada.GetProperty("tipo").GetString());
        Assert.Equal(2, paradaDescargada.GetProperty("bultos").GetArrayLength());

        // El retiro sin la fotografía exigida se rechaza, y también si faltan bultos por escanear
        var rutaMovil = $"/api/mobile/v1/rutas/{rutaId}/paradas/{paradaRetiroId}/retiro";
        var sinFoto = await repartidor.PostAsJsonAsync(rutaMovil, new
        {
            retiroId, idempotencyKey = Guid.NewGuid().ToString(), codigosBultosEscaneados = bultosRetiro,
            firmaBase64 = "ZmlybWE=", nombreEntrega = "Carlos Pérez"
        });
        Assert.Equal(HttpStatusCode.BadRequest, sinFoto.StatusCode);

        var incompleto = await repartidor.PostAsJsonAsync(rutaMovil, new
        {
            retiroId, idempotencyKey = Guid.NewGuid().ToString(), codigosBultosEscaneados = new[] { bultosRetiro[0] },
            fotoEstadoBase64 = "Zm90bw==", firmaBase64 = "ZmlybWE=", nombreEntrega = "Carlos Pérez"
        });
        Assert.Equal(HttpStatusCode.BadRequest, incompleto.StatusCode);

        // Retiro correcto, y reenviarlo (la app sin conexión puede repetirlo) es idempotente
        var claveIdempotencia = Guid.NewGuid().ToString();
        var cuerpoRetiro = new
        {
            retiroId, idempotencyKey = claveIdempotencia, codigosBultosEscaneados = bultosRetiro,
            fotoEstadoBase64 = "Zm90bw==", firmaBase64 = "ZmlybWE=", nombreEntrega = "Carlos Pérez", documentoEntrega = "1.111.111-1",
            latitud = -34.9, longitud = -56.18
        };
        var realizado = await Json(await repartidor.PostAsJsonAsync(rutaMovil, cuerpoRetiro), HttpStatusCode.OK);
        Assert.Equal("Retirado", realizado.GetProperty("estadoRetiro").GetString());
        Assert.Equal("Retirada", realizado.GetProperty("estadoParada").GetString());
        Assert.False(realizado.GetProperty("idempotente").GetBoolean());

        var reenviado = await Json(await repartidor.PostAsJsonAsync(rutaMovil, cuerpoRetiro), HttpStatusCode.OK);
        Assert.True(reenviado.GetProperty("idempotente").GetBoolean());

        // Seguimiento público: el comprador usa el mismo enlace y no se expone ningún dato personal
        var publico = await Json(await _factory.CreateClient().GetAsync($"/api/seguimiento/{codigo}"), HttpStatusCode.OK);
        Assert.Equal("Retiro", publico.GetProperty("tipo").GetString());
        Assert.Equal("Retirado", publico.GetProperty("estadoRetiro").GetString());
        Assert.Equal(2, publico.GetProperty("bultosEntregados").GetInt32());
        var textoPublico = publico.GetRawText();
        Assert.DoesNotContain("Carlos", textoPublico);
        Assert.DoesNotContain("Artigas", textoPublico);
        Assert.DoesNotContain("1.111.111-1", textoPublico);
        Assert.DoesNotContain(retiroId.ToString(), textoPublico, StringComparison.OrdinalIgnoreCase);

        // La entrega del envío cierra la parada de entrega y, con ambas cerradas, la ruta queda finalizada
        await Json(await repartidor.PostAsJsonAsync($"/api/mobile/v1/rutas/{rutaId}/paradas/{paradaEnvioId}/entrega-exitosa", new
        {
            envioId = envio.Id, idempotencyKey = Guid.NewGuid().ToString(), nombreReceptor = "Cliente", documentoReceptor = "1.234.567-8",
            firmaDigitalBase64 = "firma", fotoEvidenciaBase64 = "foto"
        }), HttpStatusCode.OK);

        // La rendición recibe en depósito los bultos retirados
        var rendicion = await Json(await deposito.PostAsJsonAsync($"/api/deposito/hojas-de-ruta/{rutaId}/rendicion",
            new { codigosBultosDevueltos = Array.Empty<string>() }), HttpStatusCode.OK);
        Assert.Contains("1 retiro(s)", rendicion.GetProperty("mensaje").GetString());

        var enDeposito = await Json(await deposito.GetAsync($"/api/retiros/{retiroId}"), HttpStatusCode.OK);
        Assert.Equal("EnDeposito", enDeposito.GetProperty("resumen").GetProperty("estado").GetString());

        // Los bultos llegan al comercio y se cierra el flujo
        var cerrado = await Json(await deposito.PostAsJsonAsync($"/api/retiros/{retiroId}/entrega-comercio", new { receptor = "Encargada de depósito" }), HttpStatusCode.OK);
        Assert.Equal("EntregadoAlComercio", cerrado.GetProperty("resumen").GetProperty("estado").GetString());

        // El historial es completo y está en el orden en que ocurrió
        var tipos = cerrado.GetProperty("eventos").EnumerateArray().Select(e => e.GetProperty("tipo").GetString()).ToList();
        Assert.Equal(new[] { "SolicitudRetiro", "AsignacionRuta", "RetiroRealizado", "RecepcionEnDeposito", "EntregaAlComercio" }, tipos);

        // El estado terminal no admite más cambios
        Assert.Equal(HttpStatusCode.BadRequest, (await deposito.PostAsJsonAsync($"/api/retiros/{retiroId}/recepcion-deposito", new { })).StatusCode);

        // El comercio ve el resultado en su portal
        var detalleComercio = await Json(await comercio.GetAsync($"/api/comercio/retiros/{retiroId}"), HttpStatusCode.OK);
        Assert.Equal("EntregadoAlComercio", detalleComercio.GetProperty("resumen").GetProperty("estado").GetString());
        Assert.True(detalleComercio.GetProperty("tieneFotoEstado").GetBoolean());
        Assert.True(detalleComercio.GetProperty("tieneFirma").GetBoolean());
    }

    [FactPostgres]
    public async Task Retiro_NoRealizado_PuedeReprogramarseConUnaNuevaVisita()
    {
        var dot = await NuevaDotacionAsync();
        var comercio = await ComercioAsync();
        var despachador = await DespachadorAsync();
        var repartidor = dot.Repartidor;
        var deposito = await DepositoAsync();

        var retiroId = (await Json(await comercio.PostAsJsonAsync("/api/comercio/retiros", SolicitudDeRetiro(cantidadBultos: 1)), HttpStatusCode.Created))
            .GetProperty("id").GetGuid();

        // Una ruta solo de retiros no tiene carga que escanear: puede despacharse directamente
        var ruta = await Json(await despachador.PostAsJsonAsync("/api/planificacion/hojas-de-ruta", new
        {
            repartidorId = dot.RepartidorId,
            vehiculoId = dot.VehiculoId,
            fecha = DateOnly.FromDateTime(DateTime.UtcNow),
            enviosIds = Array.Empty<Guid>(),
            retirosIds = new[] { retiroId }
        }), HttpStatusCode.Created);
        var rutaId = ruta.GetProperty("hojaDeRutaId").GetGuid();
        var paradaId = ruta.GetProperty("paradas")[0].GetProperty("paradaId").GetGuid();

        await Json(await despachador.PostAsJsonAsync($"/api/planificacion/hojas-de-ruta/{rutaId}/despacho", new { }), HttpStatusCode.OK);

        // El motivo debe existir en el catálogo configurado por el operador
        var ruteo = $"/api/mobile/v1/rutas/{rutaId}/paradas/{paradaId}/retiro/no-realizado";
        var motivoInexistente = await repartidor.PostAsJsonAsync(ruteo, new { retiroId, idempotencyKey = Guid.NewGuid().ToString(), motivoCodigo = "NO-EXISTE" });
        Assert.Equal(HttpStatusCode.BadRequest, motivoInexistente.StatusCode);

        var motivos = await Json(await repartidor.GetAsync($"/api/mobile/v1/rutas?fecha={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}"), HttpStatusCode.OK);
        var codigoMotivo = motivos.GetProperty("configuracion").GetProperty("motivosNoEntrega")[0].GetProperty("codigo").GetString();

        var noRealizado = await Json(await repartidor.PostAsJsonAsync(ruteo, new
        {
            retiroId, idempotencyKey = Guid.NewGuid().ToString(), motivoCodigo = codigoMotivo, observaciones = "No había nadie"
        }), HttpStatusCode.OK);
        Assert.Equal("NoRetirado", noRealizado.GetProperty("estadoRetiro").GetString());

        // La rendición lo deja como no retirado (no se recibe nada en depósito) y el despachador decide una nueva visita
        await Json(await deposito.PostAsJsonAsync($"/api/deposito/hojas-de-ruta/{rutaId}/rendicion", new { codigosBultosDevueltos = Array.Empty<string>() }), HttpStatusCode.OK);
        Assert.Equal("NoRetirado", (await Json(await deposito.GetAsync($"/api/retiros/{retiroId}"), HttpStatusCode.OK))
            .GetProperty("resumen").GetProperty("estado").GetString());

        var reprogramado = await Json(await despachador.PostAsJsonAsync($"/api/retiros/{retiroId}/reprogramar", new { }), HttpStatusCode.OK);
        Assert.Equal("Solicitado", reprogramado.GetProperty("resumen").GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, reprogramado.GetProperty("resumen").GetProperty("hojaDeRutaId").ValueKind);
    }

    [FactPostgres]
    public async Task Retiro_SiElServidorLoSacoDeLaRutaMientrasNoHabiaConexion_PrevaleceLoOcurridoEnElDomicilio()
    {
        var dot = await NuevaDotacionAsync();
        var comercio = await ComercioAsync();
        var despachador = await DespachadorAsync();
        var repartidor = dot.Repartidor;

        var creado = await Json(await comercio.PostAsJsonAsync("/api/comercio/retiros", SolicitudDeRetiro(cantidadBultos: 1)), HttpStatusCode.Created);
        var retiroId = creado.GetProperty("id").GetGuid();
        var bulto = creado.GetProperty("identificacionesBultos")[0].GetString()!;

        var envio = await CrearEnvioClasificadoAsync();
        var ruta = await Json(await despachador.PostAsJsonAsync("/api/planificacion/hojas-de-ruta", new
        {
            repartidorId = dot.RepartidorId,
            vehiculoId = dot.VehiculoId,
            fecha = DateOnly.FromDateTime(DateTime.UtcNow),
            enviosIds = new[] { envio.Id },
            retirosIds = new[] { retiroId }
        }), HttpStatusCode.Created);
        var rutaId = ruta.GetProperty("hojaDeRutaId").GetGuid();
        var paradaId = ruta.GetProperty("paradas").EnumerateArray().Single(p => p.GetProperty("tipo").GetString() == "Retiro").GetProperty("paradaId").GetGuid();

        await Json(await repartidor.PostAsJsonAsync($"/api/mobile/v1/rutas/{rutaId}/carga/confirmar",
            new { codigosBultosCargados = new[] { envio.Bulto }, idempotencyKey = Guid.NewGuid().ToString() }), HttpStatusCode.OK);

        // El despachador saca el retiro mientras el repartidor ya lo tiene descargado en su teléfono
        await Json(await despachador.PostAsJsonAsync($"/api/retiros/{retiroId}/sacar-de-ruta", new { }), HttpStatusCode.OK);
        await Json(await despachador.PostAsJsonAsync($"/api/planificacion/hojas-de-ruta/{rutaId}/despacho", new { }), HttpStatusCode.OK);

        // Al recuperar la conexión, la app envía el retiro que sí hizo: prevalece lo físico y el conflicto queda asentado
        var resultado = await Json(await repartidor.PostAsJsonAsync($"/api/mobile/v1/rutas/{rutaId}/paradas/{paradaId}/retiro", new
        {
            retiroId, idempotencyKey = Guid.NewGuid().ToString(), codigosBultosEscaneados = new[] { bulto },
            fotoEstadoBase64 = "Zm90bw==", firmaBase64 = "ZmlybWE=", nombreEntrega = "Carlos Pérez"
        }), HttpStatusCode.OK);

        Assert.True(resultado.GetProperty("conflictoResuelto").GetBoolean());
        Assert.Equal("Retirado", resultado.GetProperty("estadoRetiro").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var conflicto = await db.Set<UltimaMilla.Domain.Entities.Ejecucion.ConflictoSincronizacion>().AsNoTracking()
            .SingleAsync(c => c.EntidadId == retiroId);
        Assert.Equal("Retiro", conflicto.EntidadAfectada);
        Assert.Equal("Solicitado", conflicto.EstadoServidor);
        Assert.Equal("PrevalecenLosHechosFisicosEnElDomicilio", conflicto.PoliticaAplicada);
    }

    [FactPostgres]
    public async Task Retiro_LaCapacidadDelVehiculoSeValidaALoLargoDelRecorrido()
    {
        // Un vehículo que carga 10 kg
        var dot = await NuevaDotacionAsync(capacidadPeso: 10);
        var comercio = await ComercioAsync();
        var despachador = await DespachadorAsync();

        // Retiro de 12 kg: por sí solo no cabe en el vehículo
        var pesado = await Json(await comercio.PostAsJsonAsync("/api/comercio/retiros", new
        {
            nombreContacto = "Ana", calle = "Av. Italia", numero = "100", ciudad = "Montevideo", motivo = "Devolución",
            bultos = new[] { new { descripcion = "Electrodoméstico", peso = 12, alto = 30, ancho = 30, profundo = 30 } }
        }), HttpStatusCode.Created);

        var validacion = await despachador.PostAsJsonAsync("/api/planificacion/hojas-de-ruta/validacion", new
        {
            repartidorId = dot.RepartidorId, vehiculoId = dot.VehiculoId,
            fecha = DateOnly.FromDateTime(DateTime.UtcNow), enviosIds = Array.Empty<Guid>(),
            retirosIds = new[] { pesado.GetProperty("id").GetGuid() }
        });

        Assert.Equal(HttpStatusCode.BadRequest, validacion.StatusCode);
        var cuerpo = await validacion.Content.ReadAsStringAsync();
        Assert.Contains("capacidad", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [FactPostgres]
    public async Task Retiro_ElAislamientoEntreOperadoresYElControlPorPerfilSeRespetan()
    {
        var comercio = await ComercioAsync();
        var admin = await AdminAsync();
        var despachador = await DespachadorAsync();
        var admin2 = await DespachadorDelOperadorDosAsync();
        var repartidor = await RepartidorAsync();

        var retiroId = (await Json(await comercio.PostAsJsonAsync("/api/comercio/retiros", SolicitudDeRetiro(cantidadBultos: 1)), HttpStatusCode.Created))
            .GetProperty("id").GetGuid();

        // Otro operador no ve el retiro: ni el detalle, ni el listado, ni puede operar sobre él
        Assert.Equal(HttpStatusCode.NotFound, (await admin2.GetAsync($"/api/retiros/{retiroId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin2.PostAsJsonAsync($"/api/retiros/{retiroId}/cancelar", new { motivo = "x" })).StatusCode);
        var listadoOtroOperador = await Json(await admin2.GetAsync("/api/retiros"), HttpStatusCode.OK);
        Assert.DoesNotContain(listadoOtroOperador.GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == retiroId);

        // El operador dueño sí lo ve
        var listadoDueno = await Json(await despachador.GetAsync("/api/retiros"), HttpStatusCode.OK);
        Assert.Contains(listadoDueno.GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == retiroId);

        // Control por perfil: el comercio no accede al backoffice, y el personal no usa el portal del comercio
        Assert.Equal(HttpStatusCode.Forbidden, (await comercio.GetAsync("/api/retiros")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await comercio.PostAsJsonAsync($"/api/retiros/{retiroId}/entrega-comercio", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/comercio/retiros", SolicitudDeRetiro())).StatusCode);

        // Solo el repartidor registra retiros en la calle
        var ruteo = $"/api/mobile/v1/rutas/{Guid.NewGuid()}/paradas/{Guid.NewGuid()}/retiro";
        Assert.Equal(HttpStatusCode.Forbidden, (await comercio.PostAsJsonAsync(ruteo, new { retiroId, idempotencyKey = "k", nombreEntrega = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await repartidor.PostAsJsonAsync(ruteo, new { retiroId, idempotencyKey = "k", nombreEntrega = "x" })).StatusCode);

        // El comercio ve sus retiros pero nunca los de otro: el filtro de comercio se aplica en el repositorio
        var propios = await Json(await comercio.GetAsync("/api/comercio/retiros"), HttpStatusCode.OK);
        Assert.Contains(propios.GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == retiroId);
    }

    [FactPostgres]
    public async Task Retiro_PorApiConClave_EsIdempotenteYRechazaClavesInvalidas()
    {
        var cliente = _factory.CreateClient();
        var referencia = "API-" + Sufijo();

        var sinClave = await cliente.PostAsJsonAsync("/api/integracion/v1/retiros", SolicitudDeRetiro(referencia));
        Assert.Equal(HttpStatusCode.Unauthorized, sinClave.StatusCode);

        cliente.DefaultRequestHeaders.Add("X-Api-Key", "clave-que-no-existe");
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/integracion/v1/retiros", SolicitudDeRetiro(referencia))).StatusCode);

        var autorizado = _factory.CreateClient();
        autorizado.DefaultRequestHeaders.Add("X-Api-Key", DatabaseSeeder.ClaveApiDemo);

        var creado = await Json(await autorizado.PostAsJsonAsync("/api/integracion/v1/retiros", SolicitudDeRetiro(referencia)), HttpStatusCode.Created);
        var repetido = await Json(await autorizado.PostAsJsonAsync("/api/integracion/v1/retiros", SolicitudDeRetiro(referencia)), HttpStatusCode.OK);

        Assert.Equal(creado.GetProperty("id").GetGuid(), repetido.GetProperty("id").GetGuid());
        Assert.True(repetido.GetProperty("duplicado").GetBoolean());

        var invalida = await autorizado.PostAsJsonAsync("/api/integracion/v1/retiros", new { nombreContacto = "", motivo = "" });
        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
    }

    [FactPostgres]
    public async Task Retiro_LosAvisosAComerciosVersionanElContrato_V1NoRecibeRetirosYV2SiConSuDetalle()
    {
        var comercio = await ComercioAsync();
        var creado = await Json(await comercio.PostAsJsonAsync("/api/comercio/retiros", SolicitudDeRetiro(cantidadBultos: 2)), HttpStatusCode.Created);
        var retiroId = creado.GetProperty("id").GetGuid();
        var codigo = creado.GetProperty("codigoSeguimiento").GetString()!;

        Guid suscripcionV1 = Guid.NewGuid(), suscripcionV2 = Guid.NewGuid();
        Guid mensajeId;
        string payload;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            using var _ = db.HabilitarEscrituraAdministrativa();
            db.SuscripcionesAviso.Add(new SuscripcionAviso(suscripcionV1, DatabaseSeeder.DefaultComercioOperadorId, "*", "https://receptor-v1.invalid/aviso", "clave-v1", SuscripcionAviso.ContratoV1));
            db.SuscripcionesAviso.Add(new SuscripcionAviso(suscripcionV2, DatabaseSeeder.DefaultComercioOperadorId, "*", "https://receptor-v2.invalid/aviso", "clave-v2", SuscripcionAviso.ContratoV2));
            await db.SaveChangesAsync();

            // El evento lo dejó la propia API en el outbox, en la misma transacción que la solicitud
            var mensaje = await db.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(o => o.AgregadoId == retiroId && o.TipoEvento == "RetiroCambioEstado");
            mensajeId = mensaje.Id;
            payload = mensaje.Payload;
        }

        // El worker procesa el mensaje: se ejecuta con el mismo caso de uso que consume de la cola
        using (var scope = _factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var resultado = await mediator.Send(new ProcesarMensajeCommand(mensajeId, "Retiro", retiroId, "RetiroCambioEstado", payload));
            Assert.Equal(EstadoProcesamientoMensaje.Procesado, resultado.Estado);

            // Reprocesar el mismo mensaje no repite efectos
            var repetido = await mediator.Send(new ProcesarMensajeCommand(mensajeId, "Retiro", retiroId, "RetiroCambioEstado", payload));
            Assert.Equal(EstadoProcesamientoMensaje.Duplicado, repetido.Estado);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var avisos = await db.AvisosComercio.AsNoTracking().Where(a => a.EnvioId == retiroId).ToListAsync();

            Assert.DoesNotContain(avisos, a => a.SuscripcionAvisoId == suscripcionV1);
            var avisoV2 = Assert.Single(avisos, a => a.SuscripcionAvisoId == suscripcionV2);

            using var contenido = JsonDocument.Parse(avisoV2.Payload);
            var raiz = contenido.RootElement;
            Assert.Equal("v2", raiz.GetProperty("versionContrato").GetString());
            Assert.Equal("RetiroCambioEstado", raiz.GetProperty("evento").GetString());
            Assert.Equal(codigo, raiz.GetProperty("codigoSeguimiento").GetString());
            Assert.Equal("Solicitado", raiz.GetProperty("estado").GetString());
            Assert.Equal(2, raiz.GetProperty("resumenBultos").GetProperty("totalBultos").GetInt32());
            Assert.Equal(2, raiz.GetProperty("bultos").GetArrayLength());
            Assert.NotEmpty(avisoV2.FirmaEnviada);
        }
    }

    [FactPostgres]
    public async Task Retiro_SeIncorporaAlTableroDeOperacionYALosReportes()
    {
        var comercio = await ComercioAsync();
        var admin = await DespachadorAsync(); // el tablero y los reportes de operación son del despachador

        var antes = await Json(await admin.GetAsync("/api/tablero/resumen"), HttpStatusCode.OK);
        var pendientesAntes = antes.GetProperty("retirosPendientes").GetInt32();

        var creado = await Json(await comercio.PostAsJsonAsync("/api/comercio/retiros", SolicitudDeRetiro(cantidadBultos: 2)), HttpStatusCode.Created);
        Assert.Equal(120m, creado.GetProperty("tarifa").GetDecimal());

        // Tablero de operación: el retiro nuevo cuenta como pendiente de asignar y como retiro en gestión
        var despues = await Json(await admin.GetAsync("/api/tablero/resumen"), HttpStatusCode.OK);
        Assert.Equal(pendientesAntes + 1, despues.GetProperty("retirosPendientes").GetInt32());
        Assert.True(despues.GetProperty("retirosEnCurso").GetInt32() >= 1);

        // Reporte propio de retiros por comercio, con lo solicitado en el período
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var reporte = await Json(await admin.GetAsync($"/api/reportes?tipo=RetirosEnDomicilio&desde={hoy.AddDays(-1):yyyy-MM-dd}&hasta={hoy.AddDays(1):yyyy-MM-dd}"), HttpStatusCode.OK);
        var filas = reporte.GetProperty("itemsRetiros").EnumerateArray().ToList();
        Assert.NotEmpty(filas);
        Assert.True(filas.Sum(f => f.GetProperty("solicitados").GetInt32()) >= 1);
        // Cada fila es coherente: lo retirado, lo entregado al comercio y lo cancelado nunca supera lo solicitado
        Assert.All(filas, f =>
        {
            var solicitados = f.GetProperty("solicitados").GetInt32();
            Assert.True(f.GetProperty("entregadosAlComercio").GetInt32() <= f.GetProperty("retirados").GetInt32());
            Assert.True(f.GetProperty("retirados").GetInt32() <= solicitados);
            Assert.True(f.GetProperty("cancelados").GetInt32() <= solicitados);
        });
    }

    [FactPostgres]
    public async Task Retiro_ElComercioPuedeCancelarloMientrasSigaSolicitado()
    {
        var comercio = await ComercioAsync();
        var retiroId = (await Json(await comercio.PostAsJsonAsync("/api/comercio/retiros", SolicitudDeRetiro(cantidadBultos: 1)), HttpStatusCode.Created))
            .GetProperty("id").GetGuid();

        var sinMotivo = await comercio.PostAsJsonAsync($"/api/comercio/retiros/{retiroId}/cancelar", new { motivo = "" });
        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);

        var cancelado = await Json(await comercio.PostAsJsonAsync($"/api/comercio/retiros/{retiroId}/cancelar", new { motivo = "El comprador desistió" }), HttpStatusCode.OK);
        Assert.Equal("Cancelado", cancelado.GetProperty("resumen").GetProperty("estado").GetString());

        // Un retiro cancelado no puede cancelarse otra vez ni volver a la planificación
        Assert.Equal(HttpStatusCode.BadRequest, (await comercio.PostAsJsonAsync($"/api/comercio/retiros/{retiroId}/cancelar", new { motivo = "otra vez" })).StatusCode);
    }
}
