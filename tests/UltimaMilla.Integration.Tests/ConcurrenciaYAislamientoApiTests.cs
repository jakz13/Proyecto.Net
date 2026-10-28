using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Commands.Identidad.IniciarSesion;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Contracts.Modelos.Reportes;
using UltimaMilla.Frontend.Contracts.Modelos.Rutas;
using UltimaMilla.Frontend.Infrastructure.Http;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Garantías transversales del módulo de operación sobre Postgres real: exclusión mutua en la asignación de envíos (RF-15),
/// aislamiento entre operadores (6.5) y el estado terminal de extraviado (RF-11).
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class ConcurrenciaYAislamientoApiTests
{
    private readonly ApiFactory _factory;

    public ConcurrenciaYAislamientoApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];

    private async Task<(Guid Id, string Codigo)> CrearEnvioClasificadoAsync(HttpClient? cliente = null)
    {
        cliente ??= _factory.CreateClient();

        var alta = await cliente.PostAsJsonAsync("/api/envios", new
        {
            modalidadServicio = "Estandar", nombreDestinatario = "Cliente " + Sufijo(), calle = "Av. Rivera", numero = "2500", ciudad = "Montevideo",
            bultos = new[] { new { peso = 2, alto = 10, ancho = 10, profundo = 10 } }
        });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var json = await alta.Content.ReadFromJsonAsync<JsonElement>();
        var id = json.GetProperty("id").GetGuid();
        var bulto = json.GetProperty("bultos")[0].GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync($"/api/deposito/envios/{id}/recepcion", new { codigosBultosEscaneados = new[] { bulto } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync($"/api/deposito/envios/{id}/clasificacion",
            new { bultos = new[] { new { codigoBulto = bulto, prioridad = "Normal" } } })).StatusCode);

        return (id, json.GetProperty("codigoSeguimiento").GetString()!);
    }

    private async Task<(VehiculoDto Vehiculo, RepartidorDto Repartidor)> CrearRecursosAsync()
    {
        var flota = new HttpFlotaApi(_factory.CreateClient());
        var vehiculo = await flota.CrearVehiculoAsync(new CrearVehiculoDto { Patente = "C" + Sufijo().ToUpperInvariant(), Tipo = "Furgón", CapacidadPeso = 500, CapacidadVolumen = 5 });
        var repartidor = await flota.CrearRepartidorAsync(new CrearRepartidorDto
        {
            NombreCompleto = "Repartidor " + Sufijo(), Email = $"carrera.{Sufijo()}@ultimamilla.com.uy", Documento = "4.111.222-3", Telefono = "099000222"
        });
        return (vehiculo, repartidor);
    }

    private async Task<HttpClient> ClienteDelOperadorDosAsync()
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new IniciarSesionRequest("admin2@gmail.com", "Admin.2026!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var sesion = await login.Content.ReadFromJsonAsync<IniciarSesionResultadoDto>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion!.Token);
        return cliente;
    }

    [FactPostgres]
    public async Task DosDespachadoresArmanRutasConElMismoEnvioAlMismoTiempo_SoloUnoLoLogra()
    {
        for (var ronda = 0; ronda < 3; ronda++)
        {
            var (envioId, _) = await CrearEnvioClasificadoAsync();
            var (vehiculoA, repartidorA) = await CrearRecursosAsync();
            var (vehiculoB, repartidorB) = await CrearRecursosAsync();

            ArmarHojaDeRutaDto Pedido(VehiculoDto vehiculo, RepartidorDto repartidor) => new()
            {
                RepartidorId = repartidor.Id, VehiculoId = vehiculo.Id,
                Fecha = DateOnly.FromDateTime(DateTime.UtcNow), EnviosIds = new List<Guid> { envioId }
            };

            var despachadorA = new HttpRutasApi(_factory.CreateClient());
            var despachadorB = new HttpRutasApi(_factory.CreateClient());

            var resultados = await Task.WhenAll(
                Task.Run(() => despachadorA.ArmarYConfirmarRutaAsync(Pedido(vehiculoA, repartidorA))),
                Task.Run(() => despachadorB.ArmarYConfirmarRutaAsync(Pedido(vehiculoB, repartidorB))));

            Assert.Equal(1, resultados.Count(r => r.Exitoso));

            // Queda una sola asignación vigente y una sola parada abierta para el envío
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.AsignacionesEnvioRuta.AsNoTracking().CountAsync(a => a.EnvioId == envioId && a.Vigente));
            Assert.Equal(1, await db.Paradas.AsNoTracking().CountAsync(p => p.EnvioId == envioId && p.Estado != "Cancelada"));
        }
    }

    [FactPostgres]
    public async Task OperadorDos_NoVeNiPuedeTocarLosDatosDelOperadorUno()
    {
        var operadorUno = _factory.CreateClient();
        var (envioId, _) = await CrearEnvioClasificadoAsync(operadorUno);
        var incidencia = await operadorUno.PostAsJsonAsync("/api/backoffice/incidencias", new { tipo = "AveriaVehiculo", descripcion = "Incidencia del operador uno " + Sufijo() });
        Assert.Equal(HttpStatusCode.Created, incidencia.StatusCode);
        var incidenciaId = (await incidencia.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var operadorDos = await ClienteDelOperadorDosAsync();

        // Escrituras sobre un envío ajeno: acceso denegado y el envío queda intacto
        Assert.Equal(HttpStatusCode.Forbidden, (await operadorDos.PostAsJsonAsync($"/api/deposito/envios/{envioId}/extravio", new { motivo = "intento ajeno" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operadorDos.PostAsJsonAsync($"/api/deposito/envios/{envioId}/recepcion", new { codigosBultosEscaneados = new[] { "X" } })).StatusCode);

        // Lecturas: ninguna lista del operador dos contiene datos del uno
        var pendientes = await operadorDos.GetFromJsonAsync<JsonElement>("/api/planificacion/envios-pendientes");
        Assert.DoesNotContain(pendientes.EnumerateArray(), e => e.GetProperty("id").GetGuid() == envioId);

        var incidenciasDos = await operadorDos.GetFromJsonAsync<JsonElement>("/api/backoffice/incidencias");
        Assert.DoesNotContain(incidenciasDos.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == incidenciaId);
        Assert.Equal(HttpStatusCode.Forbidden, (await operadorDos.GetAsync($"/api/backoffice/incidencias/{incidenciaId}")).StatusCode);
        var incidenciasUno = await operadorUno.GetFromJsonAsync<JsonElement>("/api/backoffice/incidencias");
        Assert.Contains(incidenciasUno.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == incidenciaId);

        var repartidoresUno = (await operadorUno.GetFromJsonAsync<JsonElement>("/api/v1/flota/repartidores")).EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToHashSet();
        var repartidoresDos = (await operadorDos.GetFromJsonAsync<JsonElement>("/api/v1/flota/repartidores")).EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToHashSet();
        Assert.NotEmpty(repartidoresUno);
        Assert.Empty(repartidoresUno.Intersect(repartidoresDos));

        // Reportes, tablero y liquidaciones: el operador uno tiene actividad hoy; el dos no ve nada de ella
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var filtro = new FiltroReporteDto { TipoReporte = "VolumenPeriodo", FechaDesde = hoy.AddDays(-1), FechaHasta = hoy.AddDays(1) };
        var volumenUno = await new HttpReportesApi(operadorUno).GenerarReporteAsync(filtro);
        var volumenDos = await new HttpReportesApi(operadorDos).GenerarReporteAsync(filtro);
        Assert.True(volumenUno.ItemsVolumen.Sum(v => v.CantidadGuias) >= 1);
        Assert.Equal(0, volumenDos.ItemsVolumen.Sum(v => v.CantidadGuias));

        var liquidacionDos = await new HttpReportesApi(operadorDos).GenerarReporteAsync(filtro with { TipoReporte = "LiquidacionComercio" });
        Assert.Empty(liquidacionDos.ItemsLiquidacion);

        var tablero = await new HttpTableroApi(operadorDos).ObtenerResumenEnVivoAsync();
        Assert.Empty(tablero.Vehiculos);

        var liquidaciones = await operadorDos.GetFromJsonAsync<JsonElement>("/api/liquidaciones");
        Assert.Equal(0, liquidaciones.GetArrayLength());

        // El envío del operador uno sigue exactamente como estaba
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(UltimaMilla.Domain.Enums.EstadoEnvio.EnDeposito, (await db.Envios.AsNoTracking().SingleAsync(e => e.Id == envioId)).EstadoActual);
    }

    [FactPostgres]
    public async Task EnvioExtraviado_SeRetiraDeSuRutaYQuedaEnEstadoTerminal()
    {
        var cliente = _factory.CreateClient();
        var rutas = new HttpRutasApi(_factory.CreateClient());
        var (vehiculo, repartidor) = await CrearRecursosAsync();
        var (envioId, codigo) = await CrearEnvioClasificadoAsync();

        var armado = await rutas.ArmarYConfirmarRutaAsync(new ArmarHojaDeRutaDto
        {
            RepartidorId = repartidor.Id, VehiculoId = vehiculo.Id,
            Fecha = DateOnly.FromDateTime(DateTime.UtcNow), EnviosIds = new List<Guid> { envioId }
        });
        Assert.True(armado.Exitoso, armado.Mensaje);

        // El motivo es obligatorio
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync($"/api/deposito/envios/{envioId}/extravio", new { motivo = " " })).StatusCode);

        var respuesta = await cliente.PostAsJsonAsync($"/api/deposito/envios/{envioId}/extravio", new { motivo = "No apareció al cargar el vehículo" });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Extraviado", resultado.GetProperty("estado").GetString());
        Assert.True(resultado.GetProperty("retiradoDeRuta").GetBoolean());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var envio = await db.Envios.AsNoTracking().SingleAsync(e => e.Id == envioId);
            Assert.Equal(UltimaMilla.Domain.Enums.EstadoEnvio.Extraviado, envio.EstadoActual);
            Assert.Null(envio.HojaDeRutaId);
            Assert.Contains(envio.Eventos, e => e.TipoEvento == "EnvioExtraviado");
            Assert.Equal(0, await db.AsignacionesEnvioRuta.AsNoTracking().CountAsync(a => a.EnvioId == envioId && a.Vigente));
            Assert.Equal(0, await db.Paradas.AsNoTracking().CountAsync(p => p.EnvioId == envioId && p.Estado != "Cancelada"));
            Assert.Contains(await db.OutboxMessages.AsNoTracking().Where(m => m.AgregadoId == envioId).ToListAsync(), m => m.TipoEvento == "EnvioExtraviadoIntegrationEvent");
        }

        // Estado terminal: no admite nuevas transiciones y el destinatario ve el estado
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync($"/api/deposito/envios/{envioId}/extravio", new { motivo = "otra vez" })).StatusCode);
        var seguimiento = await cliente.GetFromJsonAsync<JsonElement>($"/api/seguimiento/{codigo}");
        var estado = seguimiento.GetProperty("estadoActual");
        var texto = estado.ValueKind == JsonValueKind.String ? estado.GetString() : ((UltimaMilla.Domain.Enums.EstadoEnvio)estado.GetInt32()).ToString();
        Assert.Equal("Extraviado", texto);
    }
}
