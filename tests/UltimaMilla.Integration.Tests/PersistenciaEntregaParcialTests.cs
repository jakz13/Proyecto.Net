using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// La entrega parcial (RF 31 a RF 36) debe sobrevivir a un reinicio y verse igual desde cualquier instancia de la API y desde el worker.
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
            var prueba = new PruebaDeEntrega(intento.Id, "Firma", nombreReceptor: "Receptor", documentoReceptor: "1.234.567-8",
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
}
