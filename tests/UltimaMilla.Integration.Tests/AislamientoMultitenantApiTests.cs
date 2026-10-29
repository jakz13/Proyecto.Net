using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Infrastructure.Persistence;
using DatabaseSeeder = UltimaMilla.Infrastructure.Persistence.DatabaseSeeder;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Aislamiento entre inquilinos (ADR-002) contra Postgres real: los filtros globales de <c>AppDbContext</c> acotan cada consulta al
/// operador del inquilino actual en modo fail-closed (sin operador válido devuelven 0 filas), y los flujos transversales legítimos
/// (seguimiento público, clave de API, listado de operadores activos, login y semillas) siguen funcionando mediante puertos explícitos.
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

        public Guid OperadorId { get; private set; }
        public Guid? UsuarioId => null;
        public Guid? ComercioOperadorId => null;
        public bool IsAuthenticated { get; private set; }

        public void EstablecerOperadorDeProceso(Guid operadorId)
        {
            OperadorId = operadorId;
            IsAuthenticated = true;
        }
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

        var regla1 = await db.ReglasOperativas.IgnoreQueryFilters().SingleAsync(r => r.Id == DatabaseSeeder.DefaultReglaOperativaId);
        var regla2 = await db.ReglasOperativas.IgnoreQueryFilters().SingleAsync(r => r.Id == DatabaseSeederSegundoOperador.ReglaOperativaId);
        Assert.NotEqual(regla1.MaxIntentosEntrega, regla2.MaxIntentosEntrega);

        Assert.Equal(2, await db.Usuarios.CountAsync(u => u.OperadorId == Operador2));
        Assert.True(await db.CuadrosTarifarios.IgnoreQueryFilters().AnyAsync(c => c.OperadorId == Operador2));
        Assert.True(await db.ZonasCobertura.IgnoreQueryFilters().AnyAsync(z => z.OperadorId == Operador2));
        // La base es compartida con otros tests que dan de alta envíos del operador 2: se verifican las semillas por código
        Assert.True(await db.Envios.IgnoreQueryFilters().AnyAsync(e => e.OperadorId == Operador2 && e.CodigoSeguimientoPublico == DatabaseSeederSegundoOperador.CodigoEnvio1));
        Assert.True(await db.Envios.IgnoreQueryFilters().AnyAsync(e => e.OperadorId == Operador2 && e.CodigoSeguimientoPublico == DatabaseSeederSegundoOperador.CodigoEnvio2));
    }

    [FactPostgres]
    public async Task Semilla_EsIdempotente_NoDuplicaAlVolverASembrar()
    {
        using var scope = _factory.Services.CreateScope();
        using var db = ContextoDe(scope, null);
        var antes = await db.Envios.IgnoreQueryFilters().CountAsync(e => e.OperadorId == Operador2);

        await DatabaseSeederSegundoOperador.SembrarAsync(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        Assert.Equal(antes, await db.Envios.IgnoreQueryFilters().CountAsync(e => e.OperadorId == Operador2));
        Assert.Equal(1, await db.Operadores.CountAsync(o => o.Id == Operador2));
        Assert.Equal(1, await db.ReglasOperativas.IgnoreQueryFilters().CountAsync(r => r.Id == DatabaseSeederSegundoOperador.ReglaOperativaId));
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
    public async Task Lectura_SinInquilinoAutenticadoONuloOVacio_FallaCerradoYDevuelveCeroRegistros()
    {
        using var scope = _factory.Services.CreateScope();

        // 1. _currentTenant == null
        using (var dbSinInquilino = ContextoDe(scope, null))
        {
            Assert.Empty(await dbSinInquilino.Envios.ToListAsync());
            Assert.Empty(await dbSinInquilino.HojasDeRuta.ToListAsync());
            Assert.Empty(await dbSinInquilino.ReglasOperativas.ToListAsync());
            Assert.Empty(await dbSinInquilino.ZonasCobertura.ToListAsync());
            Assert.Empty(await dbSinInquilino.ComerciosOperadores.ToListAsync());
            Assert.Empty(await dbSinInquilino.CuentasCorrientes.ToListAsync());
            Assert.Empty(await dbSinInquilino.Liquidaciones.ToListAsync());
            Assert.Empty(await dbSinInquilino.LotesImportacion.ToListAsync());
            Assert.Empty(await dbSinInquilino.SuscripcionesAviso.ToListAsync());
        }

        // 2. IsAuthenticated == false (aunque tenga un OperadorId cargado)
        using (var dbNoAutenticado = ContextoDe(scope, Operador1, autenticado: false))
        {
            Assert.Empty(await dbNoAutenticado.Envios.ToListAsync());
            Assert.Empty(await dbNoAutenticado.ReglasOperativas.ToListAsync());
            Assert.Empty(await dbNoAutenticado.SuscripcionesAviso.ToListAsync());
        }

        // 3. Autenticado pero con OperadorId == Guid.Empty
        using (var dbOperadorVacio = ContextoDe(scope, Guid.Empty, autenticado: true))
        {
            Assert.Empty(await dbOperadorVacio.Envios.ToListAsync());
            Assert.Empty(await dbOperadorVacio.ReglasOperativas.ToListAsync());
            Assert.Empty(await dbOperadorVacio.SuscripcionesAviso.ToListAsync());
        }
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
        var envio = await db.Envios.IgnoreQueryFilters().AsNoTracking().SingleAsync(e => e.ReferenciaExterna == referencia);
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
        Assert.Equal(1, await db.Envios.IgnoreQueryFilters().CountAsync(e => e.ReferenciaExterna == referencia));
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

    [FactPostgres]
    public async Task Escritura_InsertarEntidadDeOtroOperador_SeRechazaYNoPersiste()
    {
        using var scope = _factory.Services.CreateScope();
        using var dbOperador1 = ContextoDe(scope, Operador1);
        var idIntruso = Guid.NewGuid();

        // Autenticado como Operador 1, intenta insertar un recurso declarando Operador 2
        dbOperador1.MotivosNoEntrega.Add(new MotivoNoEntrega(idIntruso, Operador2, "INTRUSO", "Intento de inserción cruzada"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => dbOperador1.SaveChangesAsync());

        using var dbSinFiltro = ContextoDe(scope, null);
        Assert.False(await dbSinFiltro.MotivosNoEntrega.IgnoreQueryFilters().AnyAsync(m => m.Id == idIntruso));
    }

    [FactPostgres]
    public async Task Escritura_ModificarFilaPersistidaDeOtroOperadorFalsificandoOperadorIdEnMemoria_SeRechazaYNoAlteraLaBase()
    {
        using var scope = _factory.Services.CreateScope();
        using var dbOperador1 = ContextoDe(scope, Operador1);

        // Ataque por adjunción (Attach/Update): conoce el Id real de la zona del Operador 2 pero falsifica OperadorId = Operador 1 en memoria
        var zonaFalsificada = new ZonaCobertura(
            DatabaseSeederSegundoOperador.ZonaId,
            Operador1,
            "Zona Saboteada Por Operador 1",
            "[[-34.84, -56.03], [-34.83, -55.98], [-34.87, -55.96], [-34.84, -56.03]]");

        dbOperador1.ZonasCobertura.Update(zonaFalsificada);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => dbOperador1.SaveChangesAsync());

        // La fila real en PostgreSQL conserva su operador original y su nombre intacto
        using var dbSinFiltro = ContextoDe(scope, null);
        var zonaPersistida = await dbSinFiltro.ZonasCobertura.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(z => z.Id == DatabaseSeederSegundoOperador.ZonaId);
        Assert.Equal(Operador2, zonaPersistida.OperadorId);
        Assert.Equal("Canelones Metropolitano", zonaPersistida.Nombre);
    }

    [FactPostgres]
    public async Task Escritura_EliminarFilaPersistidaDeOtroOperadorFalsificandoOperadorIdEnMemoria_SeRechazaYNoBorraLaFila()
    {
        using var scope = _factory.Services.CreateScope();
        using var dbOperador1 = ContextoDe(scope, Operador1);

        // Conoce el Id de la zona del Operador 2 e intenta eliminarla adjuntándola como si fuera del Operador 1
        var zonaFalsificada = new ZonaCobertura(
            DatabaseSeederSegundoOperador.ZonaId,
            Operador1,
            "Canelones Metropolitano",
            "[[-34.84, -56.03], [-34.83, -55.98], [-34.87, -55.96], [-34.84, -56.03]]");

        dbOperador1.ZonasCobertura.Remove(zonaFalsificada);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => dbOperador1.SaveChangesAsync());

        using var dbSinFiltro = ContextoDe(scope, null);
        Assert.True(await dbSinFiltro.ZonasCobertura.IgnoreQueryFilters().AnyAsync(z => z.Id == DatabaseSeederSegundoOperador.ZonaId && z.OperadorId == Operador2));
    }

    [FactPostgres]
    public async Task Worker_EstablecerOperadorDeProceso_ActivaFiltroDeLecturaYBlindajeDeEscrituraSinHttpContext()
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Sin HttpContext antes de fijar el operador del proceso, el contexto no está autenticado:
        // las lecturas devuelven 0 filas (fail-closed) y las escrituras se rechazan
        Assert.False(tenant.IsAuthenticated);
        Assert.Empty(await db.Envios.AsNoTracking().ToListAsync());
        var intentoSinContexto = new MotivoNoEntrega(Guid.NewGuid(), Operador2, "SIN_CTX", "Intento antes de fijar operador");
        db.MotivosNoEntrega.Add(intentoSinContexto);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync());
        db.Entry(intentoSinContexto).State = EntityState.Detached;

        // El Worker fija explícitamente el Operador 2 para el alcance actual
        tenant.EstablecerOperadorDeProceso(Operador2);
        Assert.True(tenant.IsAuthenticated);
        Assert.Equal(Operador2, tenant.OperadorId);

        // Las lecturas quedan acotadas exclusivamente al Operador 2
        var enviosVisibles = await db.Envios.AsNoTracking().ToListAsync();
        Assert.NotEmpty(enviosVisibles);
        Assert.All(enviosVisibles, e => Assert.Equal(Operador2, e.OperadorId));

        // Y las escrituras rechazan cualquier intento de tocar recursos del Operador 1
        db.MotivosNoEntrega.Add(new MotivoNoEntrega(Guid.NewGuid(), Operador1, "CRUZADO", "No permitido desde alcance de Operador 2"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync());
    }

    [FactPostgres]
    public async Task Escritura_ContextoAutenticadoConOperadorVacio_RechazaInsercionModificacionYEliminacion()
    {
        using var scope = _factory.Services.CreateScope();

        // 1. Inserción con contexto autenticado pero OperadorId == Guid.Empty
        using (var dbOperadorVacio = ContextoDe(scope, Guid.Empty, autenticado: true))
        {
            dbOperadorVacio.MotivosNoEntrega.Add(new MotivoNoEntrega(Guid.NewGuid(), Operador1, "OP_VACIO_ADD", "No debe persistirse"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => dbOperadorVacio.SaveChangesAsync());
        }

        // 2. Modificación por adjunción con contexto autenticado pero OperadorId == Guid.Empty
        using (var dbOperadorVacio = ContextoDe(scope, Guid.Empty, autenticado: true))
        {
            var zona = new ZonaCobertura(
                DatabaseSeederSegundoOperador.ZonaId,
                Operador2,
                "Intento Con Operador Vacio",
                "[[-34.84, -56.03], [-34.83, -55.98], [-34.87, -55.96], [-34.84, -56.03]]");
            dbOperadorVacio.ZonasCobertura.Update(zona);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => dbOperadorVacio.SaveChangesAsync());
        }

        // 3. Eliminación con contexto autenticado pero OperadorId == Guid.Empty
        using (var dbOperadorVacio = ContextoDe(scope, Guid.Empty, autenticado: true))
        {
            var zona = new ZonaCobertura(
                DatabaseSeederSegundoOperador.ZonaId,
                Operador2,
                "Canelones Metropolitano",
                "[[-34.84, -56.03], [-34.83, -55.98], [-34.87, -55.96], [-34.84, -56.03]]");
            dbOperadorVacio.ZonasCobertura.Remove(zona);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => dbOperadorVacio.SaveChangesAsync());
        }

        using var dbVerificacion = ContextoDe(scope, null);
        var persistida = await dbVerificacion.ZonasCobertura.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(z => z.Id == DatabaseSeederSegundoOperador.ZonaId);
        Assert.Equal("Canelones Metropolitano", persistida.Nombre);
    }

    [FactPostgres]
    public async Task Escritura_ContextoSinOperadorAutenticado_RechazaEscriturasArbitrariasSalvoAlcanceAdministrativo()
    {
        using var scope = _factory.Services.CreateScope();

        // 1. Sin inquilino (currentTenant == null) y sin alcance administrativo: rechaza escrituras incluso con OperadorId válido
        using (var dbSinInquilino = ContextoDe(scope, null))
        {
            dbSinInquilino.MotivosNoEntrega.Add(new MotivoNoEntrega(Guid.NewGuid(), Operador1, "SIN_TENANT", "No autorizado"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => dbSinInquilino.SaveChangesAsync());
        }

        // 2. Inquilino no autenticado (IsAuthenticated == false) y sin alcance administrativo: rechaza inserción, modificación y borrado
        using (var dbNoAutenticado = ContextoDe(scope, Operador1, autenticado: false))
        {
            dbNoAutenticado.MotivosNoEntrega.Add(new MotivoNoEntrega(Guid.NewGuid(), Operador1, "NO_AUTH", "No autorizado"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => dbNoAutenticado.SaveChangesAsync());
        }

        using (var dbNoAutenticado = ContextoDe(scope, Operador2, autenticado: false))
        {
            var zona = await dbNoAutenticado.ZonasCobertura.IgnoreQueryFilters().SingleAsync(z => z.Id == DatabaseSeederSegundoOperador.ZonaId);
            zona.ActualizarDatos("Intento Sin Autenticar", zona.ColorHex);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => dbNoAutenticado.SaveChangesAsync());
        }
    }

    [FactPostgres]
    public async Task Escritura_IdentificadoresDeTenantVacios_SeRechazanInclusoConAlcanceAdministrativo()
    {
        using var scope = _factory.Services.CreateScope();

        // 1. Entidad IMustHaveTenant cuyo OperadorId se vacía en el ChangeTracker es rechazada incluso con alcance administrativo
        using (var dbAdmin = ContextoDe(scope, null))
        using (dbAdmin.HabilitarEscrituraAdministrativa())
        {
            var motivo = new MotivoNoEntrega(Guid.NewGuid(), Operador1, "VACIO_OP", "Motivo con operador vacío");
            dbAdmin.MotivosNoEntrega.Add(motivo);
            dbAdmin.Entry(motivo).Property(nameof(IMustHaveTenant.OperadorId)).CurrentValue = Guid.Empty;

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => dbAdmin.SaveChangesAsync());
        }

        // 2. Entidad IMustHaveComercioTenant cuyo ComercioOperadorId se vacía en el ChangeTracker es rechazada incluso con alcance administrativo
        using (var dbAdmin = ContextoDe(scope, null))
        using (dbAdmin.HabilitarEscrituraAdministrativa())
        {
            var suscripcion = new SuscripcionAviso(
                Guid.NewGuid(),
                DatabaseSeeder.DefaultComercioOperadorId,
                "*",
                "https://ejemplo.com/webhook",
                "secreto-prueba-1234");
            dbAdmin.SuscripcionesAviso.Add(suscripcion);
            dbAdmin.Entry(suscripcion).Property(nameof(IMustHaveComercioTenant.ComercioOperadorId)).CurrentValue = Guid.Empty;

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => dbAdmin.SaveChangesAsync());
        }
    }

    [FactPostgres]
    public async Task Escritura_EntidadDeComercio_ImpideCambiarComercioOperadorPropietarioEnMemoriaOPorAdjuncion()
    {
        using var scope = _factory.Services.CreateScope();
        var suscripcionId = Guid.NewGuid();

        // Alta legítima de una SuscripcionAviso (IMustHaveComercioTenant) bajo un operador autenticado válido
        using (var dbOperador1 = ContextoDe(scope, Operador1))
        {
            dbOperador1.SuscripcionesAviso.Add(new SuscripcionAviso(
                suscripcionId,
                DatabaseSeeder.DefaultComercioOperadorId,
                "*",
                "https://comercio1.ejemplo.com/webhook",
                "secreto-comercio-1"));
            await dbOperador1.SaveChangesAsync();
        }

        // 1. Intento de cambiar ComercioOperadorId sobre la entidad rastreada
        using (var dbOperador1 = ContextoDe(scope, Operador1))
        {
            var rastreada = await dbOperador1.SuscripcionesAviso.SingleAsync(s => s.Id == suscripcionId);
            rastreada.ActualizarWebhook("https://comercio1.ejemplo.com/webhook-v2", "secreto-comercio-1");
            dbOperador1.Entry(rastreada).Property(nameof(IMustHaveComercioTenant.ComercioOperadorId)).CurrentValue =
                DatabaseSeederSegundoOperador.ComercioOperadorId;

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => dbOperador1.SaveChangesAsync());
        }

        // 2. Intento de modificar por adjunción (Attach/Update) falsificando ComercioOperadorId en memoria
        using (var dbOperador1 = ContextoDe(scope, Operador1))
        {
            var falsificada = new SuscripcionAviso(
                suscripcionId,
                DatabaseSeederSegundoOperador.ComercioOperadorId,
                "*",
                "https://atacante.ejemplo.com/webhook",
                "secreto-atacante");
            dbOperador1.SuscripcionesAviso.Update(falsificada);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => dbOperador1.SaveChangesAsync());
        }

        // La suscripción persistida conserva su ComercioOperadorId y su URL original
        using (var dbVerificacion = ContextoDe(scope, null))
        {
            var persistida = await dbVerificacion.SuscripcionesAviso.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == suscripcionId);
            Assert.Equal(DatabaseSeeder.DefaultComercioOperadorId, persistida.ComercioOperadorId);
            Assert.Equal("https://comercio1.ejemplo.com/webhook", persistida.UrlWebhook);
        }
    }

    [FactPostgres]
    public async Task EntidadesDeComercio_AislamientoEnLecturaYEscrituraPorOperador()
    {
        using var scope = _factory.Services.CreateScope();
        var suscripcionOp1Id = Guid.NewGuid();

        using (var dbOperador1 = ContextoDe(scope, Operador1))
        {
            dbOperador1.SuscripcionesAviso.Add(new SuscripcionAviso(
                suscripcionOp1Id,
                DatabaseSeeder.DefaultComercioOperadorId,
                "*",
                "https://comercio-op1.ejemplo.com/webhook",
                "secreto-op1-1234"));
            await dbOperador1.SaveChangesAsync();
        }

        // El Operador 1 ve la suscripción de su comercio
        using (var dbOperador1 = ContextoDe(scope, Operador1))
        {
            Assert.NotNull(await dbOperador1.SuscripcionesAviso.FirstOrDefaultAsync(s => s.Id == suscripcionOp1Id));
        }

        // El Operador 2 NO puede leer la suscripción del comercio del Operador 1
        using (var dbOperador2 = ContextoDe(scope, Operador2))
        {
            Assert.Null(await dbOperador2.SuscripcionesAviso.FirstOrDefaultAsync(s => s.Id == suscripcionOp1Id));
        }

        // El Operador 1 NO puede insertar una suscripción apuntando al comercio del Operador 2
        using (var dbOperador1 = ContextoDe(scope, Operador1))
        {
            dbOperador1.SuscripcionesAviso.Add(new SuscripcionAviso(
                Guid.NewGuid(),
                DatabaseSeederSegundoOperador.ComercioOperadorId,
                "*",
                "https://intruso-op1.ejemplo.com/webhook",
                "secreto-intruso"));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => dbOperador1.SaveChangesAsync());
        }
    }

    [FactPostgres]
    public async Task OperacionesLegitimasSinTenant_LoginListadoOperadoresYSeguimiento_FuncionanConFiltroCerrado()
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        Assert.False(tenant.IsAuthenticated);

        // 1. Login: búsqueda de usuario por correo sin tenant activo
        var usuarioRepo = scope.ServiceProvider.GetRequiredService<IUsuarioRepository>();
        var adminOp2 = await usuarioRepo.ObtenerPorEmailAsync(DatabaseSeederSegundoOperador.EmailAdmin);
        Assert.NotNull(adminOp2);
        Assert.Equal(Operador2, adminOp2.OperadorId);

        // 2. Worker: listado de operadores activos sin tenant activo
        var procesosRepo = scope.ServiceProvider.GetRequiredService<IProcesosProgramadosRepository>();
        var operadoresActivos = await procesosRepo.ListarOperadoresActivosAsync();
        Assert.Contains(Operador1, operadoresActivos);
        Assert.Contains(Operador2, operadoresActivos);

        // 3. Seguimiento público por código sin tenant activo
        var envioRepo = scope.ServiceProvider.GetRequiredService<IEnvioRepository>();
        var envioOp2 = await envioRepo.GetByCodigoSeguimientoAsync(DatabaseSeederSegundoOperador.CodigoEnvio1);
        Assert.NotNull(envioOp2);
        Assert.Equal(Operador2, envioOp2.OperadorId);
    }
}
