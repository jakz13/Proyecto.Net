using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>El intento de entrega guarda en la base la versión de la regla con la que se procesó.</summary>
[Collection(ColeccionApi.Nombre)]
public class TrazabilidadReglaEnIntentoTests
{
    private readonly ApiFactory _factory;

    public TrazabilidadReglaEnIntentoTests(ApiFactory factory) => _factory = factory;

    private AppDbContext NuevoContexto(IServiceScope scope)
    {
        _ = _factory.CreateClient(); // asegura que el host arrancó y aplicó migraciones y semillas
        var opciones = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
        return new AppDbContext(opciones, null);
    }

    [FactPostgres]
    public async Task IntentoEntrega_ConservaLaReglaOperativaConLaQueSeProceso()
    {
        var id = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        using (var db = NuevoContexto(scope))
        {
            db.IntentosEntrega.Add(new IntentoEntrega(id, DatabaseSeeder.Parada1Id, DatabaseSeeder.Envio1Id,
                resultado: "Fallido", origenDatos: "MobileApp", reglaOperativaId: DatabaseSeeder.DefaultReglaOperativaId));
            using var _ = db.HabilitarEscrituraAdministrativa();
            await db.SaveChangesAsync();
        }

        using var scope2 = _factory.Services.CreateScope();
        using var db2 = NuevoContexto(scope2);
        var leido = await db2.IntentosEntrega.IgnoreQueryFilters().AsNoTracking().SingleAsync(i => i.Id == id);

        Assert.Equal(DatabaseSeeder.DefaultReglaOperativaId, leido.ReglaOperativaId);
    }

    [FactPostgres]
    public async Task IntentoEntrega_ConReglaInexistente_LaBaseLoRechaza()
    {
        using var scope = _factory.Services.CreateScope();
        using var db = NuevoContexto(scope);
        db.IntentosEntrega.Add(new IntentoEntrega(Guid.NewGuid(), DatabaseSeeder.Parada1Id, DatabaseSeeder.Envio1Id,
            resultado: "Fallido", origenDatos: "MobileApp", reglaOperativaId: Guid.NewGuid()));
        using var _ = db.HabilitarEscrituraAdministrativa();

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
