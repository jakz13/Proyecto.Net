using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Ports;
using UltimaMilla.Infrastructure.Persistence;
using DatabaseSeeder = UltimaMilla.Infrastructure.Persistence.DatabaseSeeder;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Aislamiento entre inquilinos (ADR-002) contra Postgres real: los filtros globales de <c>AppDbContext</c> acotan cada consulta al
/// operador del inquilino actual, sin depender de que cada handler compare <c>OperadorId</c> a mano, y los flujos sin inquilino
/// (seguimiento público, clave de API, Worker) siguen funcionando entre operadores.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class AislamientoMultitenantApiTests
{
    private static readonly Guid Operador1 = DatabaseSeeder.DefaultOperadorId;
    private static readonly Guid Operador2 = DatabaseSeederSegundoOperador.OperadorId;

    private readonly ApiFactory _factory;

    public AislamientoMultitenantApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Inquilino fijo para armar un <c>AppDbContext</c> como lo vería una petición autenticada de ese operador.</summary>
    private sealed class InquilinoDePrueba : ICurrentTenant
    {
        public InquilinoDePrueba(Guid operadorId, bool autenticado = true)
        {
            OperadorId = operadorId;
            IsAuthenticated = autenticado;
        }

        public Guid OperadorId { get; }
        public Guid? UsuarioId => null;
        public Guid? ComercioOperadorId => null;
        public bool IsAuthenticated { get; }
    }

    /// <summary>Asegura que el host arrancó (migraciones y semillas aplicadas) antes de consultar la base.</summary>
    private AppDbContext ContextoDe(IServiceScope scope, Guid? operadorId, bool autenticado = true)
    {
        _ = _factory.CreateClient();
        var opciones = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
        return new AppDbContext(opciones, operadorId.HasValue ? new InquilinoDePrueba(operadorId.Value, autenticado) : null);
    }

    [FactPostgres]
    public async Task Semilla_ElSegundoOperadorTieneConfiguracionPropiaYDistintaDelPrimero()
    {
        using var scope = _factory.Services.CreateScope();
        using var db = ContextoDe(scope, null);

        var regla1 = await db.ReglasOperativas.SingleAsync(r => r.Id == DatabaseSeeder.DefaultReglaOperativaId);
        var regla2 = await db.ReglasOperativas.SingleAsync(r => r.Id == DatabaseSeederSegundoOperador.ReglaOperativaId);
        Assert.NotEqual(regla1.MaxIntentosEntrega, regla2.MaxIntentosEntrega);

        Assert.Equal(2, await db.Usuarios.CountAsync(u => u.OperadorId == Operador2));
        Assert.True(await db.CuadrosTarifarios.AnyAsync(c => c.OperadorId == Operador2));
        Assert.True(await db.ZonasCobertura.AnyAsync(z => z.OperadorId == Operador2));
        // La base es compartida con otros tests que dan de alta envíos del operador 2: se verifican las semillas por código
        Assert.True(await db.Envios.AnyAsync(e => e.OperadorId == Operador2 && e.CodigoSeguimientoPublico == DatabaseSeederSegundoOperador.CodigoEnvio1));
        Assert.True(await db.Envios.AnyAsync(e => e.OperadorId == Operador2 && e.CodigoSeguimientoPublico == DatabaseSeederSegundoOperador.CodigoEnvio2));
    }

    [FactPostgres]
    public async Task Semilla_EsIdempotente_NoDuplicaAlVolverASembrar()
    {
        using var scope = _factory.Services.CreateScope();
        using var db = ContextoDe(scope, null);
        var antes = await db.Envios.CountAsync(e => e.OperadorId == Operador2);

        await DatabaseSeederSegundoOperador.SembrarAsync(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        Assert.Equal(antes, await db.Envios.CountAsync(e => e.OperadorId == Operador2));
        Assert.Equal(1, await db.Operadores.CountAsync(o => o.Id == Operador2));
        Assert.Equal(1, await db.ReglasOperativas.CountAsync(r => r.Id == DatabaseSeederSegundoOperador.ReglaOperativaId));
    }

    [FactPostgres]
    public async Task Operador1_NoVeNingunDatoDelOperador2_EnNingunaEntidadDeAlcanceDeOperador()
    {
        using var scope = _factory.Services.CreateScope();
        using var db = ContextoDe(scope, Operador1);

        Assert.DoesNotContain(await db.Envios.ToListAsync(), e => e.OperadorId == Operador2);
        Assert.DoesNotContain(await db.ReglasOperativas.ToListAsync(), r => r.OperadorId == Operador2);
        Assert.DoesNotContain(await db.CuadrosTarifarios.ToListAsync(), c => c.OperadorId == Operador2);
        Assert.DoesNotContain(await db.ZonasCobertura.ToListAsync(), z => z.OperadorId == Operador2);
        Assert.DoesNotContain(await db.MotivosNoEntrega.ToListAsync(), m => m.OperadorId == Operador2);
        Assert.DoesNotContain(await db.IdentidadesVisuales.ToListAsync(), i => i.OperadorId == Operador2);
        Assert.DoesNotContain(await db.ComerciosOperadores.ToListAsync(), c => c.OperadorId == Operador2);
        Assert.NotEmpty(await db.Envios.ToListAsync());
    }

    [FactPostgres]
    public async Task Operador2_NoVeNingunDatoDelOperador1()
    {
        using var scope = _factory.Services.CreateScope();
        using var db = ContextoDe(scope, Operador2);

        Assert.DoesNotContain(await db.Envios.ToListAsync(), e => e.OperadorId == Operador1);
        Assert.DoesNotContain(await db.HojasDeRuta.ToListAsync(), h => h.OperadorId == Operador1);
        Assert.DoesNotContain(await db.ReglasOperativas.ToListAsync(), r => r.OperadorId == Operador1);
        Assert.DoesNotContain(await db.Vehiculos.ToListAsync(), v => v.OperadorId == Operador1);
        Assert.DoesNotContain(await db.Repartidores.ToListAsync(), r => r.OperadorId == Operador1);
        Assert.NotEmpty(await db.Envios.ToListAsync());
        Assert.All(await db.Envios.ToListAsync(), e => Assert.Equal(Operador2, e.OperadorId));
    }

    [FactPostgres]
    public async Task BuscarPorIdUnRecursoDeOtroOperador_NoLoEncuentra_AunConociendoElId()
    {
        using var scope = _factory.Services.CreateScope();
        using var db = ContextoDe(scope, Operador1);

        // Anti-IDOR a nivel de persistencia: el identificador es válido pero pertenece a otro inquilino
        Assert.Null(await db.Envios.FirstOrDefaultAsync(e => e.Id == DatabaseSeederSegundoOperador.Envio1Id));
        Assert.Null(await db.ReglasOperativas.FirstOrDefaultAsync(r => r.Id == DatabaseSeederSegundoOperador.ReglaOperativaId));
        Assert.Null(await db.ZonasCobertura.FirstOrDefaultAsync(z => z.Id == DatabaseSeederSegundoOperador.ZonaId));
        Assert.Null(await db.Envios.FindAsync(Guid.Empty));
    }

    [FactPostgres]
    public async Task SinInquilinoAutenticado_ElFiltroNoRestringe_ParaElWorkerYLasSemillas()
    {
        using var scope = _factory.Services.CreateScope();
        using var db = ContextoDe(scope, Operador1, autenticado: false);

        Assert.Contains(await db.Envios.ToListAsync(), e => e.OperadorId == Operador2);
        Assert.Contains(await db.Envios.ToListAsync(), e => e.OperadorId == Operador1);
    }

    [FactPostgres]
    public async Task IgnoreQueryFilters_PermiteLaLecturaEntreOperadoresDeFormaExplicita()
    {
        using var scope = _factory.Services.CreateScope();
        using var db = ContextoDe(scope, Operador1);

        Assert.Contains(await db.Envios.IgnoreQueryFilters().ToListAsync(), e => e.OperadorId == Operador2);
    }

    [FactPostgres]
    public async Task Seguimiento_PublicoYAnonimo_ResuelveEnviosDeCualquierOperador()
    {
        // Sin cabeceras de inquilino la petición resuelve al operador por defecto: el código público debe alcanzar igual al envío del operador 2
        var respuesta = await _factory.CreateClient().GetAsync($"/api/seguimiento/{DatabaseSeederSegundoOperador.CodigoEnvio1}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(DatabaseSeederSegundoOperador.CodigoEnvio1, cuerpo.GetProperty("codigoSeguimiento").GetString());
    }

    [FactPostgres]
    public async Task Seguimiento_CodigoInexistente_RespondeNotFoundGenerico()
    {
        var respuesta = await _factory.CreateClient().GetAsync("/api/seguimiento/ENV-9999-99999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [FactPostgres]
    public async Task AltaPorApi_ConLaClaveDelComercioDelOperador2_SeCreaEnEseOperadorConSuTarifa()
    {
        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", DatabaseSeederSegundoOperador.ClaveApiDemo);
        var referencia = "IT-OP2-" + Guid.NewGuid().ToString("N")[..8];

        var respuesta = await cliente.PostAsJsonAsync("/api/integracion/v1/envios", new
        {
            modalidadServicio = "Estandar",
            nombreDestinatario = "Cliente Express",
            calle = "Av. Giannattasio",
            numero = "2000",
            ciudad = "Canelones",
            bultos = new[] { new { peso = 2, alto = 10, ancho = 10, profundo = 10 } },
            referenciaExterna = referencia
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        using var scope = _factory.Services.CreateScope();
        using var db = ContextoDe(scope, null);
        var envio = await db.Envios.AsNoTracking().SingleAsync(e => e.ReferenciaExterna == referencia);
        Assert.Equal(Operador2, envio.OperadorId);
        Assert.Equal(DatabaseSeederSegundoOperador.ComercioOperadorId, envio.ComercioOperadorId);
        Assert.Equal(200m, envio.TarifaCalculada); // tarifa del operador 2 (el operador 1 cobra 150)
    }

    [FactPostgres]
    public async Task AltaPorApi_ReintentoConLaMismaReferenciaDelOperador2_NoDuplica()
    {
        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", DatabaseSeederSegundoOperador.ClaveApiDemo);
        var referencia = "IT-OP2-IDEM-" + Guid.NewGuid().ToString("N")[..8];
        var cuerpo = new
        {
            modalidadServicio = "Estandar",
            nombreDestinatario = "Cliente Express",
            calle = "Calle",
            numero = "1",
            ciudad = "Canelones",
            bultos = new[] { new { peso = 2, alto = 10, ancho = 10, profundo = 10 } },
            referenciaExterna = referencia
        };

        var primera = await cliente.PostAsJsonAsync("/api/integracion/v1/envios", cuerpo);
        var segunda = await cliente.PostAsJsonAsync("/api/integracion/v1/envios", cuerpo);

        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        using var scope = _factory.Services.CreateScope();
        using var db = ContextoDe(scope, null);
        Assert.Equal(1, await db.Envios.CountAsync(e => e.ReferenciaExterna == referencia));
    }

    [FactPostgres]
    public async Task AltaPorApi_ConClaveInvalida_SigueRechazandose()
    {
        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", "clave-inventada");

        var respuesta = await cliente.PostAsJsonAsync("/api/integracion/v1/envios", new
        {
            modalidadServicio = "Estandar",
            nombreDestinatario = "X",
            calle = "Calle",
            numero = "1",
            ciudad = "Montevideo",
            bultos = new[] { new { peso = 2, alto = 10, ancho = 10, profundo = 10 } }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
