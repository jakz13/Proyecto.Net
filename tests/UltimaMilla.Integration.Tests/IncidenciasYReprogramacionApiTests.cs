using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>Incidencias y reprogramación por el destinatario contra Postgres real.</summary>
[Collection(ColeccionApi.Nombre)]
public class IncidenciasYReprogramacionApiTests
{
    private const string CodigoEnDeposito = "ENV-2026-00111";
    private static readonly Guid DireccionDelEnvio = Guid.Parse("eeeeeeee-0004-0000-0000-000000000004");

    private readonly ApiFactory _factory;

    public IncidenciasYReprogramacionApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [FactPostgres]
    public async Task Incidencia_CicloCompleto_AbreSigueYCierraConBitacora()
    {
        var cliente = _factory.CreateClient();

        var abierta = await cliente.PostAsJsonAsync("/api/backoffice/incidencias",
            new { tipo = "AveriaVehiculo", descripcion = "Se pinchó una rueda", severidad = "Alta" });
        Assert.Equal(HttpStatusCode.Created, abierta.StatusCode);
        var id = (await abierta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var seguimiento = await cliente.PostAsJsonAsync($"/api/backoffice/incidencias/{id}/seguimiento",
            new { comentario = "Llamé al taller", tomarACargo = true });
        Assert.Equal(HttpStatusCode.OK, seguimiento.StatusCode);

        var cierre = await cliente.PostAsJsonAsync($"/api/backoffice/incidencias/{id}/cierre", new { resolucion = "Cambio de rueda" });
        Assert.Equal(HttpStatusCode.OK, cierre.StatusCode);

        var detalle = await cliente.GetFromJsonAsync<JsonElement>($"/api/backoffice/incidencias/{id}");
        Assert.Equal("Cerrada", detalle.GetProperty("estado").GetString());
        Assert.Equal(3, detalle.GetProperty("bitacora").GetArrayLength());

        var cerrarOtraVez = await cliente.PostAsJsonAsync($"/api/backoffice/incidencias/{id}/cierre", new { resolucion = "otra" });
        Assert.Equal(HttpStatusCode.BadRequest, cerrarOtraVez.StatusCode);
        var notaEnCerrada = await cliente.PostAsJsonAsync($"/api/backoffice/incidencias/{id}/seguimiento", new { comentario = "tarde" });
        Assert.Equal(HttpStatusCode.BadRequest, notaEnCerrada.StatusCode);
    }

    [FactPostgres]
    public async Task Incidencia_TextosDemasiadoLargos_Dan400()
    {
        var cliente = _factory.CreateClient();

        var tipoLargo = await cliente.PostAsJsonAsync("/api/backoffice/incidencias", new { tipo = new string('t', 60), descripcion = "x" });
        var descripcionLarga = await cliente.PostAsJsonAsync("/api/backoffice/incidencias", new { tipo = "Otro", descripcion = new string('d', 1100) });

        Assert.Equal(HttpStatusCode.BadRequest, tipoLargo.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, descripcionLarga.StatusCode);
    }

    [FactPostgres]
    public async Task Incidencia_ConEnvioOHojaInexistentes_Da404()
    {
        var cliente = _factory.CreateClient();

        var envio = await cliente.PostAsJsonAsync("/api/backoffice/incidencias",
            new { tipo = "Reclamo", descripcion = "x", envioId = Guid.NewGuid() });
        var ruta = await cliente.PostAsJsonAsync("/api/backoffice/incidencias",
            new { tipo = "Reclamo", descripcion = "x", hojaDeRutaId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, envio.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ruta.StatusCode);
    }

    private async Task<(Guid Manana, Guid Tarde)> PrepararFranjasAsync()
    {
        var zona = Guid.NewGuid();
        var manana = new FranjaHoraria(Guid.NewGuid(), zona, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0));
        var tarde = new FranjaHoraria(Guid.NewGuid(), zona, DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Las franjas pertenecen a una zona de cobertura real del operador
        using var _ = db.HabilitarEscrituraAdministrativa();
        db.ZonasCobertura.Add(new ZonaCobertura(zona, DatabaseSeeder.DefaultOperadorId, "Zona de prueba " + zona.ToString("N")[..8], "[[0,0],[1,1],[2,2]]"));
        db.FranjasHorarias.AddRange(manana, tarde);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Direcciones\" SET \"ZonaCoberturaId\" = {zona} WHERE \"Id\" = {DireccionDelEnvio}");
        // Estado inicial limpio para que cada test sea independiente del orden
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"SolicitudesReprogramacion\"");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Envios\" SET \"FranjaHorariaComprometidaId\" = NULL WHERE \"CodigoSeguimientoPublico\" = {CodigoEnDeposito}");
        await db.SaveChangesAsync();
        return (manana.Id, tarde.Id);
    }

    [FactPostgres]
    public async Task Reprogramacion_FranjaDeLaZona_SeAceptaYQuedaRegistrada()
    {
        var (manana, tarde) = await PrepararFranjasAsync();
        var cliente = _factory.CreateClient();

        var franjas = await cliente.GetFromJsonAsync<JsonElement>($"/api/seguimiento/{CodigoEnDeposito}/franjas");
        Assert.True(franjas.GetProperty("puedeReprogramar").GetBoolean());
        Assert.Equal(2, franjas.GetProperty("franjas").GetArrayLength());

        var aceptada = await cliente.PostAsJsonAsync($"/api/seguimiento/{CodigoEnDeposito}/reprogramacion", new { franjaHorariaId = tarde });
        Assert.Equal(HttpStatusCode.OK, aceptada.StatusCode);
        Assert.True((await aceptada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("aceptada").GetBoolean());

        var misma = await cliente.PostAsJsonAsync($"/api/seguimiento/{CodigoEnDeposito}/reprogramacion", new { franjaHorariaId = tarde });
        var cuerpo = await misma.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(cuerpo.GetProperty("aceptada").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var envio = await db.Envios.IgnoreQueryFilters().AsNoTracking().SingleAsync(e => e.CodigoSeguimientoPublico == CodigoEnDeposito);
        Assert.Equal(tarde, envio.FranjaHorariaComprometidaId);
        Assert.Contains(envio.Eventos, e => e.TipoEvento == "ReprogramacionSolicitadaPorDestinatario");
        Assert.Equal(1, await db.SolicitudesReprogramacion.CountAsync(s => s.Estado == "Aprobada" && s.EnvioId == envio.Id));
        Assert.NotEqual(manana, envio.FranjaHorariaComprometidaId);
    }

    [FactPostgres]
    public async Task Reprogramacion_FranjaInexistente_SeRechazaConMotivo()
    {
        await PrepararFranjasAsync();

        var respuesta = await _factory.CreateClient().PostAsJsonAsync(
            $"/api/seguimiento/{CodigoEnDeposito}/reprogramacion", new { franjaHorariaId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(cuerpo.GetProperty("aceptada").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(cuerpo.GetProperty("motivo").GetString()));
    }

    [FactPostgres]
    public async Task Reprogramacion_EnvioEnTransito_SeRechazaYCodigoInexistenteDa404()
    {
        var (_, tarde) = await PrepararFranjasAsync();
        var cliente = _factory.CreateClient();

        var enTransito = await cliente.PostAsJsonAsync("/api/seguimiento/ENV-2026-00108/reprogramacion", new { franjaHorariaId = tarde });
        var inexistente = await cliente.PostAsJsonAsync("/api/seguimiento/NO-EXISTE/reprogramacion", new { franjaHorariaId = tarde });

        Assert.Equal(HttpStatusCode.OK, enTransito.StatusCode);
        Assert.False((await enTransito.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("aceptada").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [FactPostgres]
    public async Task Reprogramacion_SolicitudesSimultaneas_NoDan500()
    {
        var (_, tarde) = await PrepararFranjasAsync();
        var cliente = _factory.CreateClient();

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            cliente.PostAsJsonAsync($"/api/seguimiento/{CodigoEnDeposito}/reprogramacion", new { franjaHorariaId = tarde })));

        Assert.All(respuestas, r => Assert.True(
            r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict, $"Respuesta inesperada: {(int)r.StatusCode}"));
    }

    [FactPostgres]
    public async Task ConflictosSincronizacion_ListaSoloLosDelOperador_YValidaAutorizacionYLímite()
    {
        var op1Cliente = _factory.CreateClient();
        var conflictoOp1Id = Guid.NewGuid();
        var conflictoOp2Id = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            using var _ = db.HabilitarEscrituraAdministrativa();

            var envioOp1 = await db.Envios.IgnoreQueryFilters().AsNoTracking()
                .FirstAsync(e => e.OperadorId == DatabaseSeeder.DefaultOperadorId);
            var envioOp2 = await db.Envios.IgnoreQueryFilters().AsNoTracking()
                .FirstAsync(e => e.OperadorId == DatabaseSeederSegundoOperador.OperadorId);

            db.ConflictosSincronizacion.AddRange(
                new UltimaMilla.Domain.Entities.Ejecucion.ConflictoSincronizacion(
                    conflictoOp1Id,
                    "Envio",
                    envioOp1.Id,
                    "EnDeposito",
                    "NoEntregado",
                    "PrevaleceEstadoServidor",
                    "Conflicto de prueba operador 1"),
                new UltimaMilla.Domain.Entities.Ejecucion.ConflictoSincronizacion(
                    conflictoOp2Id,
                    "Envio",
                    envioOp2.Id,
                    "Cancelado",
                    "Entregado",
                    "PrevaleceEntregaFisicaConEvidencia",
                    "Conflicto ajeno operador 2"));
            await db.SaveChangesAsync();
        }

        try
        {
            var respuesta = await op1Cliente.GetAsync("/api/backoffice/incidencias/conflictos?maximo=20");
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            var lista = await respuesta.Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.NotNull(lista);
            Assert.Contains(lista!, x => x.GetProperty("id").GetGuid() == conflictoOp1Id
                && x.GetProperty("politicaAplicada").GetString() == "PrevaleceEstadoServidor");
            Assert.DoesNotContain(lista!, x => x.GetProperty("id").GetGuid() == conflictoOp2Id);

            Assert.Equal(HttpStatusCode.BadRequest, (await op1Cliente.GetAsync("/api/backoffice/incidencias/conflictos?maximo=0")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await op1Cliente.GetAsync("/api/backoffice/incidencias/conflictos?maximo=201")).StatusCode);

            var tokenInvalido = _factory.CreateClient();
            tokenInvalido.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token.invalido.jwt");
            Assert.Equal(HttpStatusCode.Unauthorized, (await tokenInvalido.GetAsync("/api/backoffice/incidencias/conflictos")).StatusCode);

            var operario = await UsuariosDePrueba.OperarioAsync(_factory);
            Assert.Equal(HttpStatusCode.Forbidden, (await operario.GetAsync("/api/backoffice/incidencias/conflictos")).StatusCode);

            var op2Cliente = _factory.CreateClient();
            var loginOp2 = await op2Cliente.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = DatabaseSeederSegundoOperador.EmailDespachador,
                password = PoliticaDeSemillas.PasswordDe(DatabaseSeederAdministracion.PasswordDemo)
            });
            Assert.Equal(HttpStatusCode.OK, loginOp2.StatusCode);
            var tokenOp2 = (await loginOp2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
            op2Cliente.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenOp2);

            var respuestaOp2 = await op2Cliente.GetAsync("/api/backoffice/incidencias/conflictos?maximo=20");
            Assert.Equal(HttpStatusCode.OK, respuestaOp2.StatusCode);
            var listaOp2 = await respuestaOp2.Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.NotNull(listaOp2);
            Assert.Contains(listaOp2!, x => x.GetProperty("id").GetGuid() == conflictoOp2Id
                && x.GetProperty("politicaAplicada").GetString() == "PrevaleceEntregaFisicaConEvidencia");
            Assert.DoesNotContain(listaOp2!, x => x.GetProperty("id").GetGuid() == conflictoOp1Id);
        }
        finally
        {
            using var scopeLimpieza = _factory.Services.CreateScope();
            var dbLimpieza = scopeLimpieza.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbLimpieza.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"ConflictosSincronizacion\" WHERE \"Id\" IN ({conflictoOp1Id}, {conflictoOp2Id})");
        }
    }
}
